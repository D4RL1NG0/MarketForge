namespace AlbionMarket.Infrastructure.AlbionDataApi.Models;

public sealed class GoldPriceDto
{
    public string Server { get; set; } = "west";
    public long Price { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
