namespace AlbionMarket.Application.Features.Market.GetItems;

public class GetItemsRequest
{
    public string? Search { get; set; }
    public string? Tier { get; set; }
    public int? Enchant { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
}