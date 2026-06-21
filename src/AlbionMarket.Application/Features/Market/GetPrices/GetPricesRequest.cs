namespace AlbionMarket.Application.Features.Market.GetPrices;

public sealed class GetPricesRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string? City { get; set; }
    public int? Tier { get; set; }
    public int? Enchant { get; set; }
    public int? Quality { get; set; }
    public string? Server { get; set; }
}