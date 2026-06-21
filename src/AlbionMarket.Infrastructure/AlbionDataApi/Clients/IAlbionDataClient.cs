using AlbionMarket.Infrastructure.AlbionDataApi.Models;

public interface IAlbionDataClient
{
    Task<IReadOnlyList<MarketPriceDto>> GetPricesAsync(
        string itemId,
        IEnumerable<string> locations,
        int? quality = null,
        string? server = null);

    Task<IReadOnlyList<MarketPriceDto>> GetPricesBatchAsync(
        IEnumerable<string> itemIds,
        IEnumerable<string> locations,
        int? quality = null,
        string? server = null);

    Task<GoldPriceDto> GetLatestGoldAsync(string? server = null);
    
    Task<IReadOnlyList<ItemDto>> GetItemsAsync(string search);
}
