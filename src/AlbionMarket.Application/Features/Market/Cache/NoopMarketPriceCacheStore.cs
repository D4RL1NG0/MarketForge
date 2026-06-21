using AlbionMarket.Application.Features.Market.Models;

namespace AlbionMarket.Application.Features.Market.Cache;

public sealed class NoopMarketPriceCacheStore : IMarketPriceCacheStore
{
    public bool IsEnabled => false;

    public Task<IReadOnlyList<MarketPrice>> GetPricesAsync(string server, string itemId, int quality, IReadOnlyCollection<string> locations, bool allowStale, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MarketPrice>>(Array.Empty<MarketPrice>());

    public Task UpsertPricesAsync(string server, string itemId, int quality, IReadOnlyCollection<MarketPrice> prices, DateTimeOffset validUntilUtc, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnqueuePriceSyncAsync(string server, string itemId, int quality, int priority = 0, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<PriceSyncRequest>> ClaimPriceSyncBatchAsync(int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PriceSyncRequest>>(Array.Empty<PriceSyncRequest>());

    public Task CompletePriceSyncAsync(IEnumerable<PriceSyncRequest> requests, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RequeuePriceSyncAsync(IEnumerable<PriceSyncRequest> requests, TimeSpan delay, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<GoldPrice?> GetGoldAsync(string server, bool allowStale, CancellationToken cancellationToken = default)
        => Task.FromResult<GoldPrice?>(null);

    public Task UpsertGoldAsync(string server, GoldPrice gold, DateTimeOffset validUntilUtc, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
