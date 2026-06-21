using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class ItemSuggestion
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

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("subCategory")]
    public string SubCategory { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ItemId : Name;
    public string Subtitle => $"{ItemId}  •  T{Tier}.{Enchantment}";
}
