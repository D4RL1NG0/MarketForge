using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using AlbionMarket.Mobile.Models;

namespace AlbionMarket.Mobile.Services;

public sealed class AlbionMarketApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, CacheEntry<List<ItemSuggestion>>> ItemsCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<List<CatalogCategory>>> CategoriesCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<List<MarketPrice>>> PricesCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<MarketOpportunity?>> BestCityCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<GoldPrice?>> GoldCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<RefiningQuote?>> RefiningCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CacheEntry<CraftingQuote?>> CraftingCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<List<ItemSuggestion>> SearchItemsAsync(string baseUrl, string search, string tier, int enchant, string? category = null, string? subCategory = null, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/items", new Dictionary<string, string?>
        {
            ["search"] = search,
            ["tier"] = tier,
            ["enchant"] = enchant.ToString(),
            ["category"] = category,
            ["subCategory"] = subCategory
        });

        return await GetCachedAsync(ItemsCache, url, TimeSpan.FromMinutes(30), async () =>
            await _httpClient.GetFromJsonAsync<List<ItemSuggestion>>(url, JsonOptions, cancellationToken) ?? new List<ItemSuggestion>());
    }

    public async Task<List<CatalogCategory>> GetCategoriesAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var cleanBaseUrl = CleanBaseUrl(baseUrl);
        var url = $"{cleanBaseUrl}/api/market/categories";
        return await GetCachedAsync(CategoriesCache, url, TimeSpan.FromHours(6), async () =>
            await _httpClient.GetFromJsonAsync<List<CatalogCategory>>(url, JsonOptions, cancellationToken) ?? new List<CatalogCategory>());
    }

    public async Task<List<MarketPrice>> GetPricesAsync(string baseUrl, string itemId, int tier, int enchant, int quality, string server, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/prices", new Dictionary<string, string?>
        {
            ["itemId"] = itemId,
            ["tier"] = tier.ToString(),
            ["enchant"] = enchant.ToString(),
            ["quality"] = quality.ToString(),
            ["server"] = server
        });

        return await GetCachedUntilNextAlbionWindowAsync(PricesCache, url, async () =>
            await _httpClient.GetFromJsonAsync<List<MarketPrice>>(url, JsonOptions, cancellationToken) ?? new List<MarketPrice>());
    }

    public async Task<MarketOpportunity?> GetBestCityAsync(string baseUrl, string itemId, int tier, int enchant, int quality, string server, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/best-city", new Dictionary<string, string?>
        {
            ["itemId"] = itemId,
            ["tier"] = tier.ToString(),
            ["enchant"] = enchant.ToString(),
            ["quality"] = quality.ToString(),
            ["server"] = server
        });

        return await GetCachedUntilNextAlbionWindowAsync(BestCityCache, url, async () =>
            await _httpClient.GetFromJsonAsync<MarketOpportunity>(url, JsonOptions, cancellationToken));
    }


    public async Task<GoldPrice?> GetGoldAsync(string baseUrl, string server, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/gold", new Dictionary<string, string?>
        {
            ["server"] = server
        });

        return await GetCachedUntilNextAlbionWindowAsync(GoldCache, url, async () =>
            await _httpClient.GetFromJsonAsync<GoldPrice>(url, JsonOptions, cancellationToken));
    }


    public async Task<RefiningQuote?> GetRefiningQuoteAsync(string baseUrl, string resource, int tier, int enchant, string server, bool includeCaerleon, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/refining", new Dictionary<string, string?>
        {
            ["resource"] = resource,
            ["tier"] = tier.ToString(),
            ["enchant"] = enchant.ToString(),
            ["server"] = server,
            ["includeCaerleon"] = includeCaerleon.ToString().ToLowerInvariant()
        });

        return await GetCachedUntilNextAlbionWindowAsync(RefiningCache, url, async () =>
            await _httpClient.GetFromJsonAsync<RefiningQuote>(url, JsonOptions, cancellationToken));
    }


    public async Task<CraftingQuote?> GetCraftingQuoteAsync(string baseUrl, string itemId, int tier, int enchant, int quality, string server, bool includeCaerleon, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(baseUrl, "/api/market/crafting", new Dictionary<string, string?>
        {
            ["itemId"] = itemId,
            ["tier"] = tier.ToString(),
            ["enchant"] = enchant.ToString(),
            ["quality"] = quality.ToString(),
            ["server"] = server,
            ["includeCaerleon"] = includeCaerleon.ToString().ToLowerInvariant()
        });

        return await GetCachedUntilNextAlbionWindowAsync(CraftingCache, url, async () =>
            await _httpClient.GetFromJsonAsync<CraftingQuote>(url, JsonOptions, cancellationToken));
    }

    private static async Task<T> GetCachedAsync<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        var now = DateTimeOffset.UtcNow;
        if (cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
            return cached.Value;

        var value = await factory();
        cache[key] = new CacheEntry<T>(value, now.Add(ttl));
        return value;
    }

    private static Task<T> GetCachedUntilNextAlbionWindowAsync<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, Func<Task<T>> factory)
    {
        var expires = NextAlbionPriceRefreshWindowUtc();
        return GetCachedAsync(cache, key, expires - DateTimeOffset.UtcNow, factory);
    }

    private static DateTimeOffset NextAlbionPriceRefreshWindowUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var nextMinute = ((now.Minute / 15) + 1) * 15;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddMinutes(nextMinute);
        if (next <= now.AddSeconds(30))
            next = next.AddMinutes(15);
        return next;
    }

    private static string BuildUrl(string baseUrl, string path, Dictionary<string, string?> query)
    {
        var cleanBaseUrl = CleanBaseUrl(baseUrl);
        var parameters = query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}");

        return $"{cleanBaseUrl}{path}?{string.Join('&', parameters)}";
    }

    private static string CleanBaseUrl(string baseUrl)
    {
        var cleanBaseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(cleanBaseUrl))
            throw new InvalidOperationException("Informe a URL da API. Exemplo: http://10.0.2.2:5164");
        return cleanBaseUrl;
    }

    private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresAt);
}
