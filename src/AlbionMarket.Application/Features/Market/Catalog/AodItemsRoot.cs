using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AlbionMarket.Application.Features.Market.Catalog;

public class AodItemsRoot
{
    [JsonPropertyName("?xml")]
    public Dictionary<string, string>? XmlDeclaration { get; set; }

    [JsonPropertyName("items")]
    public AodItemsContainer? Items { get; set; }
}

public class AodItemsContainer
{
    [JsonPropertyName("shopcategories")]
    public object? ShopCategories { get; set; }

    [JsonPropertyName("hideoutitem")]
    public AodItemRaw? HideoutItem { get; set; }

    [JsonPropertyName("farmableitem")]
    public List<AodItemRaw>? FarmableItems { get; set; }

    [JsonPropertyName("simpleitem")]
    public List<AodItemRaw>? SimpleItems { get; set; }

    [JsonPropertyName("consumableitem")]
    public List<AodItemRaw>? ConsumableItems { get; set; }

    [JsonPropertyName("consumablefrominventoryitem")]
    public List<AodItemRaw>? ConsumableFromInventoryItems { get; set; }

    [JsonPropertyName("equipmentitem")]
    public List<AodItemRaw>? EquipmentItems { get; set; }

    [JsonPropertyName("furnitureitem")]
    public List<AodItemRaw>? FurnitureItems { get; set; }

    [JsonPropertyName("journalitem")]
    public List<AodItemRaw>? JournalItems { get; set; }

    [JsonPropertyName("crystalleagueitem")]
    public List<AodItemRaw>? CrystalLeagueItems { get; set; }

    /// <summary>
    /// Extrai todos os items de todas as coleções
    /// </summary>
    public IEnumerable<AodItemRaw> GetAllItems()
    {
        var items = new List<AodItemRaw>();

        if (HideoutItem != null)
            items.Add(HideoutItem);

        if (FarmableItems != null)
            items.AddRange(FarmableItems);

        if (SimpleItems != null)
            items.AddRange(SimpleItems);

        if (ConsumableItems != null)
            items.AddRange(ConsumableItems);

        if (ConsumableFromInventoryItems != null)
            items.AddRange(ConsumableFromInventoryItems);

        if (EquipmentItems != null)
            items.AddRange(EquipmentItems);

        if (FurnitureItems != null)
            items.AddRange(FurnitureItems);

        if (JournalItems != null)
            items.AddRange(JournalItems);

        if (CrystalLeagueItems != null)
            items.AddRange(CrystalLeagueItems);

        return items;
    }
}
