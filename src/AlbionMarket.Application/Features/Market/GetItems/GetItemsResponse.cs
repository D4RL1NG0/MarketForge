namespace AlbionMarket.Application.Features.Market.GetItems;

public class GetItemsResponse
{
    public string ItemId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string NamePtBr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int Enchantment { get; set; }
    public string Category { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
}