namespace AlbionMarket.Application.Features.Market.Models;

public sealed class RefiningQuote
{
    public string Resource { get; set; } = string.Empty;
    public string ResourceNamePtBr { get; set; } = string.Empty;
    public string ResourceNameEn { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int Enchantment { get; set; }
    public string Server { get; set; } = "west";
    public string RefinedItemId { get; set; } = string.Empty;
    public string RefinedItemName { get; set; } = string.Empty;
    public string RefinedItemNamePtBr { get; set; } = string.Empty;
    public long GrossIngredientCost { get; set; }
    public long TotalIngredientCost { get; set; }
    public long EffectiveIngredientCost { get; set; }
    public string RefiningCity { get; set; } = string.Empty;
    public decimal ResourceReturnRate { get; set; }
    public string BestSellCity { get; set; } = string.Empty;
    public long BestSellPrice { get; set; }
    public long NetSellWithPremium { get; set; }
    public long NetSellWithoutPremium { get; set; }
    public long ProfitWithPremium { get; set; }
    public long ProfitWithoutPremium { get; set; }
    public decimal ProfitPercentWithPremium { get; set; }
    public decimal ProfitPercentWithoutPremium { get; set; }
    public long SaleTaxWithPremium { get; set; }
    public long SaleTaxWithoutPremium { get; set; }
    public bool HasOpportunityWithPremium { get; set; }
    public bool HasOpportunityWithoutPremium { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset ValidUntilUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RefiningIngredientCost> Ingredients { get; set; } = new();
}

public sealed class RefiningIngredientCost
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemNamePtBr { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int Enchantment { get; set; }
    public int Quantity { get; set; }
    public string City { get; set; } = string.Empty;
    public long UnitPrice { get; set; }
    public long TotalPrice { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
