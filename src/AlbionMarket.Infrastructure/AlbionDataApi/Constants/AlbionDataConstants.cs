namespace AlbionMarket.Infrastructure.AlbionDataApi.Constants;

public static class AlbionDataConstants
{
    public const string WestBaseUrl = "https://west.albion-online-data.com/";
    public const string EastBaseUrl = "https://east.albion-online-data.com/";
    public const string EuropeBaseUrl = "https://europe.albion-online-data.com/";
    public const string BaseUrl = WestBaseUrl;

    public static string NormalizeServer(string? server)
    {
        return "west";
    }

    public static string GetBaseUrl(string? server)
    {
        return WestBaseUrl;
    }
}
