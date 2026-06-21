namespace AlbionMarket.Application.Features.Market.Interfaces;
using AlbionMarket.Application.Features.Market.Models;
using AlbionMarket.Application.Features.Market.GetItems;


public interface IMarketService
{
    Task<List<MarketPrice>> GetPricesAsync(string ItemId, string? city = null, int? tier = null, int? enchant = null, int? quality = null, string? server = null);

    Task<List<GetItemsResponse>> GetItemsAsync(
        string? search,
        string? tier,
        int? enchant,
        string? category = null,
        string? subCategory = null);

    Task<ItemMarketOpportunity> GetBestCityAsync(string itemId, int? tier = null, int? enchant = null, int? quality = null, string? server = null);

    Task<GoldPrice> GetLatestGoldAsync(string? server = null);

    Task<RefiningQuote> GetRefiningQuoteAsync(string resource, int tier, int enchant, string? server = null, bool includeCaerleon = false);

    Task<CraftingQuote> GetCraftingQuoteAsync(string itemId, int tier, int enchant, int quality, string? server = null, bool includeCaerleon = false);
}