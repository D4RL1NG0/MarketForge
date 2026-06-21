using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class RefiningQuote
{
    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("resourceNamePtBr")]
    public string ResourceNamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("resourceNameEn")]
    public string ResourceNameEn { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("enchantment")]
    public int Enchantment { get; set; }

    [JsonPropertyName("server")]
    public string Server { get; set; } = "west";

    [JsonPropertyName("refinedItemId")]
    public string RefinedItemId { get; set; } = string.Empty;

    [JsonPropertyName("refinedItemName")]
    public string RefinedItemName { get; set; } = string.Empty;

    [JsonPropertyName("refinedItemNamePtBr")]
    public string RefinedItemNamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("grossIngredientCost")]
    public long GrossIngredientCost { get; set; }

    [JsonPropertyName("totalIngredientCost")]
    public long TotalIngredientCost { get; set; }

    [JsonPropertyName("effectiveIngredientCost")]
    public long EffectiveIngredientCost { get; set; }

    [JsonPropertyName("refiningCity")]
    public string RefiningCity { get; set; } = string.Empty;

    [JsonPropertyName("resourceReturnRate")]
    public decimal ResourceReturnRate { get; set; }

    [JsonPropertyName("bestSellCity")]
    public string BestSellCity { get; set; } = string.Empty;

    [JsonPropertyName("bestSellPrice")]
    public long BestSellPrice { get; set; }

    [JsonPropertyName("netSellWithPremium")]
    public long NetSellWithPremium { get; set; }

    [JsonPropertyName("netSellWithoutPremium")]
    public long NetSellWithoutPremium { get; set; }

    [JsonPropertyName("profitWithPremium")]
    public long ProfitWithPremium { get; set; }

    [JsonPropertyName("profitWithoutPremium")]
    public long ProfitWithoutPremium { get; set; }

    [JsonPropertyName("profitPercentWithPremium")]
    public decimal ProfitPercentWithPremium { get; set; }

    [JsonPropertyName("profitPercentWithoutPremium")]
    public decimal ProfitPercentWithoutPremium { get; set; }

    [JsonPropertyName("saleTaxWithPremium")]
    public long SaleTaxWithPremium { get; set; }

    [JsonPropertyName("saleTaxWithoutPremium")]
    public long SaleTaxWithoutPremium { get; set; }

    [JsonPropertyName("hasOpportunityWithPremium")]
    public bool HasOpportunityWithPremium { get; set; }

    [JsonPropertyName("hasOpportunityWithoutPremium")]
    public bool HasOpportunityWithoutPremium { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [JsonPropertyName("validUntilUtc")]
    public DateTimeOffset ValidUntilUtc { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("ingredients")]
    public List<RefiningIngredientCost> Ingredients { get; set; } = new();

    public string UpdatedAtText => UpdatedAtUtc.HasValue ? UpdatedAtUtc.Value.UtcDateTime.ToString("HH:mm") : "-";
    public string ValidUntilText => ValidUntilUtc.UtcDateTime.ToString("HH:mm");
}

public sealed class RefiningIngredientCost
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("itemNamePtBr")]
    public string ItemNamePtBr { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("enchantment")]
    public int Enchantment { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("unitPrice")]
    public long UnitPrice { get; set; }

    [JsonPropertyName("totalPrice")]
    public long TotalPrice { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string UpdatedAtText => UpdatedAtUtc.HasValue ? UpdatedAtUtc.Value.UtcDateTime.ToString("HH:mm") : "-";
}
