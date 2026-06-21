using AlbionMarket.Application.Features.Market.Models;

namespace AlbionMarket.Application.Features.Market.Cache;

public interface IMarketPriceCacheStore
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<MarketPrice>> GetPricesAsync(
        string server,
        string itemId,
        int quality,
        IReadOnlyCollection<string> locations,
        bool allowStale,
        CancellationToken cancellationToken = default);

    Task UpsertPricesAsync(
        string server,
        string itemId,
        int quality,
        IReadOnlyCollection<MarketPrice> prices,
        DateTimeOffset validUntilUtc,
        CancellationToken cancellationToken = default);

    Task EnqueuePriceSyncAsync(
        string server,
        string itemId,
        int quality,
        int priority = 0,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceSyncRequest>> ClaimPriceSyncBatchAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task CompletePriceSyncAsync(
        IEnumerable<PriceSyncRequest> requests,
        CancellationToken cancellationToken = default);

    Task RequeuePriceSyncAsync(
        IEnumerable<PriceSyncRequest> requests,
        TimeSpan delay,
        CancellationToken cancellationToken = default);

    Task<GoldPrice?> GetGoldAsync(string server, bool allowStale, CancellationToken cancellationToken = default);

    Task UpsertGoldAsync(string server, GoldPrice gold, DateTimeOffset validUntilUtc, CancellationToken cancellationToken = default);
}
