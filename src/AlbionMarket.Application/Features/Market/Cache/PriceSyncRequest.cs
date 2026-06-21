namespace AlbionMarket.Application.Features.Market.Cache;

public sealed record PriceSyncRequest(string Server, string ItemId, int Quality, int Priority);
