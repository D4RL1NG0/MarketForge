using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class GoldPrice
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "west";

    [JsonPropertyName("price")]
    public long Price { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string PriceText => Price > 0 ? Price.ToString("N0") : "-";
}
