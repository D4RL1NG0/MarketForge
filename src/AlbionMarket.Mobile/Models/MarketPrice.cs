using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class MarketPrice
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("itemNamePtBr")]
    public string ItemNamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("sellPriceMin")]
    public long SellPriceMin { get; set; }

    [JsonPropertyName("sellPriceMax")]
    public long SellPriceMax { get; set; }

    [JsonPropertyName("buyPriceMin")]
    public long BuyPriceMin { get; set; }

    [JsonPropertyName("buyPriceMax")]
    public long BuyPriceMax { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string SellPriceMinText => SellPriceMin > 0 ? SellPriceMin.ToString("N0") : "-";
    public string BuyPriceMaxText => BuyPriceMax > 0 ? BuyPriceMax.ToString("N0") : "-";
    public string UpdatedAtText => UpdatedAtUtc.HasValue ? UpdatedAtUtc.Value.UtcDateTime.ToString("HH:mm") : "-";
}
