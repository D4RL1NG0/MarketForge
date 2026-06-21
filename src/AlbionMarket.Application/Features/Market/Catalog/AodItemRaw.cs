using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AlbionMarket.Application.Features.Market.Catalog;

public class AodItemRaw
{
    [JsonPropertyName("@uniquename")]
    public string UniqueName { get; set; } = "";

    [JsonPropertyName("@tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("@shopcategory")]
    public string? ShopCategory { get; set; }

    [JsonPropertyName("@shopsubcategory1")]
    public string? ShopSubCategory1 { get; set; }

    [JsonPropertyName("@shopsubcategory2")]
    public string? ShopSubCategory2 { get; set; }

    [JsonPropertyName("@slottype")]
    public string? SlotType { get; set; }

    [JsonPropertyName("@maxqualitylevel")]
    public string? MaxQualityLevel { get; set; }

    [JsonPropertyName("@localizednames")]
    public Dictionary<string, string>? LocalizedNames { get; set; }

    [JsonPropertyName("localizednames")]
    public Dictionary<string, string>? LocalizedNamesAlt { get; set; }

    [JsonIgnore]
    public Dictionary<string, string>? Names =>
        LocalizedNames ?? LocalizedNamesAlt;

    [JsonPropertyName("enchantments")]
    public AodEnchantmentWrapper? Enchantments { get; set; }

    [JsonPropertyName("@showinmarketplace")]
    public string? ShowInMarketplace { get; set; }

    [JsonPropertyName("@itempower")]
    public string? ItemPower { get; set; }
}