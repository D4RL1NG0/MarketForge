namespace AlbionMarket.Application.Features.Market.Models;

public sealed class GoldPrice
{
    public string Server { get; set; } = "west";
    public long Price { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
