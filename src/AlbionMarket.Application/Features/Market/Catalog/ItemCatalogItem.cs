public class ItemCatalogItem
{
    public string ItemId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string NamePtBr { get; set; } = string.Empty;

    public int Tier { get; set; }
    public int Enchantment { get; set; }
    public string Category { get; set; } = default!;
    public string SubCategory { get; set; } = default!;
}