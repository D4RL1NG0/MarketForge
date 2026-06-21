using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class SavedMarketItem
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("namePtBr")]
    public string NamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("enchantment")]
    public int Enchantment { get; set; }

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("subCategory")]
    public string SubCategory { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "category";

    [JsonPropertyName("savedAt")]
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
