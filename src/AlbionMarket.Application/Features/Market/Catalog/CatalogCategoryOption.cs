namespace AlbionMarket.Application.Features.Market.Catalog;

public sealed class CatalogCategoryOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<CatalogSubCategoryOption> Groups { get; set; } = new();
}

public sealed class CatalogSubCategoryOption
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
