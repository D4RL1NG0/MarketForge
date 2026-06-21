namespace AlbionMarket.Application.Features.Market.Models;

public class ItemMarketOpportunity
{
    public string ItemId { get; set; } = default!;
    public string ItemName { get; set; } = default!;
    public string ItemNamePtBr { get; set; } = string.Empty;

    public string BestSellCity { get; set; } = default!;
    public long BestSellPrice { get; set; }

    public string BestBuyCity { get; set; } = default!;
    public long BestBuyPrice { get; set; }

    public bool HasOpportunity { get; set; }
    public string Message { get; set; } = string.Empty;

    // Bruto mantém compatibilidade com versões antigas da UI.
    public long Profit => BestSellPrice > 0 && BestBuyPrice > 0 ? BestSellPrice - BestBuyPrice : 0;

    // Albion cobra taxa sobre a venda. A UI mostra os dois cenários para o usuário decidir.
    public long ProfitWithPremium => CalculateNetProfit(0.04m);
    public long ProfitWithoutPremium => CalculateNetProfit(0.08m);

    private long CalculateNetProfit(decimal taxRate)
    {
        if (BestSellPrice <= 0 || BestBuyPrice <= 0)
            return 0;

        var netSell = Math.Floor(BestSellPrice * (1m - taxRate));
        return (long)netSell - BestBuyPrice;
    }
}
