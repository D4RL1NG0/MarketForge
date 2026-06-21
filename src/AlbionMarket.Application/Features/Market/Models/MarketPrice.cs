namespace AlbionMarket.Application.Features.Market.Models;

public class MarketPrice
{
    public required string ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemNamePtBr { get; set; } = string.Empty;
    public required string City { get; set; }
    public long SellPriceMin { get; set; }
    public long SellPriceMax { get; set; }
    public long BuyPriceMin { get; set; }
    public long BuyPriceMax { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
