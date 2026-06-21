using AlbionMarket.Application.Features.Market.Common;
using AlbionMarket.Application.Features.Market.Models;
using AlbionMarket.Infrastructure.AlbionDataApi.Clients;
using AlbionMarket.Infrastructure.AlbionDataApi.Models;
using Microsoft.Extensions.Hosting;

namespace AlbionMarket.Application.Features.Market.Cache;

public sealed class PriceSyncHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan BetweenBatchesDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan GoldSyncPeriod = TimeSpan.FromMinutes(15);
    private const int BatchLimit = 8;
    private const string LaunchServer = "west";

    private readonly IMarketPriceCacheStore _store;
    private readonly IAlbionDataClient _client;
    private DateTimeOffset _nextGoldSyncAtUtc = DateTimeOffset.MinValue;

    public PriceSyncHostedService(IMarketPriceCacheStore store, IAlbionDataClient client)
    {
        _store = store;
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_store.IsEnabled)
        {
            Console.WriteLine("ℹ️ Persistent price cache not configured. Background sync disabled.");
            return;
        }

        Console.WriteLine("✅ MarketForge background price sync started in Neon-only launch mode. Users read Neon; only this worker calls Albion Data.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncGoldIfDueAsync(stoppingToken);

                var claimed = await _store.ClaimPriceSyncBatchAsync(BatchLimit, stoppingToken);
                if (claimed.Count == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                foreach (var group in claimed.GroupBy(x => new { Server = LaunchServer, x.Quality }))
                {
                    var requests = group
                        .Select(x => new PriceSyncRequest(LaunchServer, x.ItemId, x.Quality, x.Priority))
                        .ToList();

                    var pending = new List<PriceSyncRequest>();
                    foreach (var request in requests)
                    {
                        var alreadyCached = await _store.GetPricesAsync(
                            LaunchServer,
                            request.ItemId,
                            request.Quality,
                            Cities.All,
                            allowStale: false,
                            stoppingToken);

                        if (alreadyCached.Count > 0)
                            await _store.CompletePriceSyncAsync(new[] { request }, stoppingToken);
                        else
                            pending.Add(request);
                    }

                    if (pending.Count == 0)
                        continue;

                    var itemIds = pending
                        .Select(x => x.ItemId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(BatchLimit)
                        .ToArray();

                    Console.WriteLine($"ℹ️ Syncing {itemIds.Length} West item(s) from Albion Data into Neon.");
                    var prices = await _client.GetPricesBatchAsync(itemIds, Cities.All, group.Key.Quality, LaunchServer);

                    if (prices.Count == 0)
                    {
                        await _store.RequeuePriceSyncAsync(pending, ErrorDelay, stoppingToken);
                        Console.WriteLine($"⚠️ Empty/limited Albion response. Worker sleeping for {ErrorDelay.TotalMinutes:0} minutes before next sync attempt.");
                        await Task.Delay(ErrorDelay, stoppingToken);
                        break;
                    }

                    var byItem = prices
                        .Where(IsUsable)
                        .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

                    foreach (var request in pending)
                    {
                        if (!byItem.TryGetValue(request.ItemId, out var itemPrices) || itemPrices.Count == 0)
                            continue;

                        var marketPrices = itemPrices
                            .GroupBy(x => $"{x.ItemId}|{x.city}", StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.OrderByDescending(x => x.sell_price_min > 0 || x.buy_price_max > 0)
                                .ThenByDescending(x => x.sell_volume + x.buy_volume)
                                .ThenByDescending(x => Math.Max(x.sell_price_min, x.buy_price_max))
                                .First())
                            .Select(ToMarketPrice)
                            .Where(x => x.SellPriceMin > 0 || x.SellPriceMax > 0 || x.BuyPriceMin > 0 || x.BuyPriceMax > 0)
                            .ToList();

                        if (marketPrices.Count > 0)
                            await _store.UpsertPricesAsync(LaunchServer, request.ItemId, request.Quality, marketPrices, NextAlbionPriceRefreshWindowUtc(), stoppingToken);
                    }

                    await _store.CompletePriceSyncAsync(pending, stoppingToken);
                    await Task.Delay(BetweenBatchesDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Background price sync error: {ex.Message}");
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task SyncGoldIfDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_nextGoldSyncAtUtc > now)
            return;

        _nextGoldSyncAtUtc = now.Add(GoldSyncPeriod);

        var cached = await _store.GetGoldAsync(LaunchServer, false, cancellationToken);
        if (cached is not null && cached.Price > 0)
            return;

        var gold = await _client.GetLatestGoldAsync(LaunchServer);
        if (gold.Price <= 0)
            return;

        await _store.UpsertGoldAsync(LaunchServer, new GoldPrice
        {
            Server = LaunchServer,
            Price = gold.Price,
            UpdatedAtUtc = gold.UpdatedAtUtc
        }, NextAlbionPriceRefreshWindowUtc(), cancellationToken);
    }

    private static MarketPrice ToMarketPrice(MarketPriceDto x)
    {
        return new MarketPrice
        {
            ItemId = x.ItemId,
            City = x.city,
            SellPriceMin = x.sell_price_min,
            SellPriceMax = x.sell_price_max,
            BuyPriceMin = x.buy_price_min,
            BuyPriceMax = x.buy_price_max,
            UpdatedAtUtc = LatestDate(x.sell_price_min_date, x.sell_price_max_date, x.buy_price_min_date, x.buy_price_max_date)
        };
    }

    private static bool IsUsable(MarketPriceDto p)
    {
        return !string.IsNullOrWhiteSpace(p.ItemId)
            && !string.IsNullOrWhiteSpace(p.city)
            && p.sell_price_min >= 0
            && p.sell_price_max >= 0
            && p.buy_price_min >= 0
            && p.buy_price_max >= 0
            && (p.sell_price_min > 0 || p.sell_price_max > 0 || p.buy_price_min > 0 || p.buy_price_max > 0);
    }

    private static DateTimeOffset? LatestDate(params DateTimeOffset?[] values)
    {
        var valid = values.Where(x => x.HasValue).Select(x => x!.Value).OrderByDescending(x => x).ToList();
        return valid.Count == 0 ? null : valid[0];
    }

    private static DateTimeOffset NextAlbionPriceRefreshWindowUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var nextMinute = ((now.Minute / 15) + 1) * 15;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddMinutes(nextMinute);
        if (next <= now.AddSeconds(30))
            next = next.AddMinutes(15);
        return next;
    }
}
