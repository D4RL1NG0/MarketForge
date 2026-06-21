using System.Text.Json.Serialization;

namespace AlbionMarket.Infrastructure.AlbionDataApi.Models;

public class MarketPriceDto
{
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string city { get; set; } = string.Empty;

    [JsonPropertyName("quality")]
    public int quality { get; set; }

    [JsonPropertyName("sell_price_min")]
    public long sell_price_min { get; set; }

    [JsonPropertyName("sell_price_max")]
    public long sell_price_max { get; set; }

    [JsonPropertyName("buy_price_min")]
    public long buy_price_min { get; set; }

    [JsonPropertyName("buy_price_max")]
    public long buy_price_max { get; set; }

    [JsonPropertyName("sell_volume")]
    public int sell_volume { get; set; }

    [JsonPropertyName("buy_volume")]
    public int buy_volume { get; set; }

    [JsonPropertyName("sell_price_min_date")]
    public DateTimeOffset? sell_price_min_date { get; set; }

    [JsonPropertyName("sell_price_max_date")]
    public DateTimeOffset? sell_price_max_date { get; set; }

    [JsonPropertyName("buy_price_min_date")]
    public DateTimeOffset? buy_price_min_date { get; set; }

    [JsonPropertyName("buy_price_max_date")]
    public DateTimeOffset? buy_price_max_date { get; set; }
}
