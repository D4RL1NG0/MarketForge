using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class MarketOpportunity
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("itemNamePtBr")]
    public string ItemNamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("bestSellCity")]
    public string BestSellCity { get; set; } = string.Empty;

    [JsonPropertyName("bestSellPrice")]
    public long BestSellPrice { get; set; }

    [JsonPropertyName("bestBuyCity")]
    public string BestBuyCity { get; set; } = string.Empty;

    [JsonPropertyName("bestBuyPrice")]
    public long BestBuyPrice { get; set; }

    [JsonPropertyName("profit")]
    public long Profit { get; set; }

    [JsonPropertyName("profitWithPremium")]
    public long ProfitWithPremium { get; set; }

    [JsonPropertyName("profitWithoutPremium")]
    public long ProfitWithoutPremium { get; set; }

    [JsonPropertyName("hasOpportunity")]
    public bool HasOpportunity { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(ItemName) ? ItemId : ItemName;
}
