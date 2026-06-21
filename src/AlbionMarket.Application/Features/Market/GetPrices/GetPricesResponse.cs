namespace AlbionMarket.Domain.Models
{
    public class GetPricesResponse
    {
        public required string ItemId { get; set; }
        public required string City { get; set; }
        public int Quality { get; set; }

        public long SellPriceMin { get; set; }
        public long SellPriceMax { get; set; }

        public long BuyPriceMin { get; set; }
        public long BuyPriceMax { get; set; }

        public int SellVolume { get; set; }
        public int BuyVolume { get; set; }
    }
}