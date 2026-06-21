using System.Net;
using System.Text.Json;
using System.Threading;
using AlbionMarket.Infrastructure.AlbionDataApi.Constants;
using AlbionMarket.Infrastructure.AlbionDataApi.Models;

namespace AlbionMarket.Infrastructure.AlbionDataApi.Clients;

public class AlbionDataClient : IAlbionDataClient
{
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static readonly object RequestClockLock = new();
    private static readonly TimeSpan MinimumRequestSpacing = TimeSpan.FromMilliseconds(4500);
    private static readonly TimeSpan RateLimitBackoffDuration = TimeSpan.FromMinutes(10);
    private static DateTimeOffset NextAllowedRequestAt = DateTimeOffset.MinValue;
    private static DateTimeOffset RateLimitedUntil = DateTimeOffset.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public AlbionDataClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<MarketPriceDto>> GetPricesAsync(string ItemId, IEnumerable<string> locations, int? quality = null, string? server = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ItemId))
                return new List<MarketPriceDto>();

            var itemId = ItemId.Trim().ToUpperInvariant();
            var cleanLocations = locations
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (cleanLocations.Length == 0)
                return new List<MarketPriceDto>();

            var now = DateTimeOffset.UtcNow;
            if (RateLimitedUntil > now)
            {
                Console.WriteLine($"⚠️ Albion API rate-limit backoff active until {RateLimitedUntil:O}. Skipping {itemId}.");
                return new List<MarketPriceDto>();
            }

            var url = BuildPricesUrl(itemId, cleanLocations, quality, server);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("AlbionMarket/1.0");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await SendWithThrottleAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    RateLimitedUntil = DateTimeOffset.UtcNow.Add(RateLimitBackoffDuration);

                Console.WriteLine($"⚠️ Albion API returned {(int)response.StatusCode} for {url}");
                return new List<MarketPriceDto>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var prices = await JsonSerializer.DeserializeAsync<List<MarketPriceDto>>(stream, JsonOptions)
                ?? new List<MarketPriceDto>();

            if (quality.HasValue)
                prices = prices.Where(p => p.quality == quality.Value).ToList();

            return prices
                .Where(IsUsable)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"⚠️ API Error fetching prices for {ItemId}: {ex.Message}");
            return new List<MarketPriceDto>();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ JSON Error parsing prices for {ItemId}: {ex.Message}");
            return new List<MarketPriceDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error in GetPricesAsync: {ex.Message}");
            return new List<MarketPriceDto>();
        }
    }

    public async Task<IReadOnlyList<MarketPriceDto>> GetPricesBatchAsync(IEnumerable<string> itemIds, IEnumerable<string> locations, int? quality = null, string? server = null)
    {
        var cleanItemIds = itemIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cleanItemIds.Length == 0)
            return new List<MarketPriceDto>();

        // Albion Data suporta vários item ids no mesmo path. Isso reduz muito o risco de rate limit.
        // Mantemos uma margem segura para não ultrapassar URL muito grande.
        var result = new List<MarketPriceDto>();
        foreach (var chunk in ChunkForUrl(cleanItemIds, 1400))
        {
            var chunkResult = await GetPricesBatchChunkAsync(chunk, locations, quality, server);
            result.AddRange(chunkResult);
        }

        return result;
    }

    private async Task<IReadOnlyList<MarketPriceDto>> GetPricesBatchChunkAsync(IReadOnlyCollection<string> itemIds, IEnumerable<string> locations, int? quality, string? server)
    {
        try
        {
            var cleanLocations = locations
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (itemIds.Count == 0 || cleanLocations.Length == 0)
                return new List<MarketPriceDto>();

            var now = DateTimeOffset.UtcNow;
            if (RateLimitedUntil > now)
            {
                Console.WriteLine($"⚠️ Albion API rate-limit backoff active until {RateLimitedUntil:O}. Skipping batch with {itemIds.Count} items.");
                return new List<MarketPriceDto>();
            }

            var url = BuildPricesBatchUrl(itemIds, cleanLocations, quality, server);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("MarketForge/1.1");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await SendWithThrottleAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    RateLimitedUntil = DateTimeOffset.UtcNow.Add(RateLimitBackoffDuration);

                Console.WriteLine($"⚠️ Albion API returned {(int)response.StatusCode} for batch with {itemIds.Count} items");
                return new List<MarketPriceDto>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var prices = await JsonSerializer.DeserializeAsync<List<MarketPriceDto>>(stream, JsonOptions)
                ?? new List<MarketPriceDto>();

            if (quality.HasValue)
                prices = prices.Where(p => p.quality == quality.Value).ToList();

            return prices.Where(IsUsable).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ API Error fetching batch prices: {ex.Message}");
            return new List<MarketPriceDto>();
        }
    }

    public async Task<GoldPriceDto> GetLatestGoldAsync(string? server = null)
    {
        var normalizedServer = AlbionDataConstants.NormalizeServer(server);
        try
        {
            var url = BuildGoldUrl(server);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("AlbionMarket/1.0");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await SendWithThrottleAsync(request);
            if (!response.IsSuccessStatusCode)
                return new GoldPriceDto { Server = normalizedServer };

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                element = element[0];

            var price = ReadLong(element, "price", "gold_price", "goldPrice", "value", "silver");
            var updated = ReadDate(element, "timestamp", "time", "date", "updated_at", "updatedAt", "updatedAtUtc");

            return new GoldPriceDto
            {
                Server = normalizedServer,
                Price = price,
                UpdatedAtUtc = updated
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error fetching gold price for {normalizedServer}: {ex.Message}");
            return new GoldPriceDto { Server = normalizedServer };
        }
    }

    public Task<IReadOnlyList<ItemDto>> GetItemsAsync(string search)
    {
        // Os itens vêm do catálogo local XML/TXT. A API pública do Albion Data não deve ser usada como autocomplete.
        return Task.FromResult<IReadOnlyList<ItemDto>>(new List<ItemDto>());
    }


    private async Task<HttpResponseMessage> SendWithThrottleAsync(HttpRequestMessage request)
    {
        await RequestGate.WaitAsync();
        try
        {
            var nowBeforeThrottle = DateTimeOffset.UtcNow;
            if (RateLimitedUntil > nowBeforeThrottle)
            {
                // Não segura a requisição do usuário aguardando minutos; devolve 429 local
                // para o MarketService cachear negativo e parar a avalanche imediatamente.
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    RequestMessage = request,
                    ReasonPhrase = "Albion Data rate-limit backoff active"
                };
            }

            TimeSpan wait;
            lock (RequestClockLock)
            {
                var now = DateTimeOffset.UtcNow;
                wait = NextAllowedRequestAt > now ? NextAllowedRequestAt - now : TimeSpan.Zero;
                var basis = wait == TimeSpan.Zero ? now : NextAllowedRequestAt;
                NextAllowedRequestAt = basis.Add(MinimumRequestSpacing);
            }

            if (wait > TimeSpan.Zero)
                await Task.Delay(wait);

            return await _http.SendAsync(request);
        }
        finally
        {
            RequestGate.Release();
        }
    }

    private static string BuildPricesUrl(string itemId, IReadOnlyCollection<string> locations, int? quality, string? server)
    {
        var encodedItem = WebUtility.UrlEncode(itemId);
        var encodedLocations = WebUtility.UrlEncode(string.Join(',', locations));
        var baseUrl = AlbionDataConstants.GetBaseUrl(server).TrimEnd('/');
        var url = $"{baseUrl}/api/v2/stats/prices/{encodedItem}.json?locations={encodedLocations}";

        if (quality.HasValue)
            url += $"&qualities={quality.Value}";

        return url;
    }

    private static string BuildPricesBatchUrl(IReadOnlyCollection<string> itemIds, IReadOnlyCollection<string> locations, int? quality, string? server)
    {
        var encodedItems = string.Join(',', itemIds.Select(WebUtility.UrlEncode));
        var encodedLocations = WebUtility.UrlEncode(string.Join(',', locations));
        var baseUrl = AlbionDataConstants.GetBaseUrl(server).TrimEnd('/');
        var url = $"{baseUrl}/api/v2/stats/prices/{encodedItems}.json?locations={encodedLocations}";

        if (quality.HasValue)
            url += $"&qualities={quality.Value}";

        return url;
    }

    private static IEnumerable<IReadOnlyCollection<string>> ChunkForUrl(IReadOnlyCollection<string> itemIds, int maxEncodedLength)
    {
        var chunk = new List<string>();
        var length = 0;
        foreach (var itemId in itemIds)
        {
            var encodedLength = WebUtility.UrlEncode(itemId).Length + 1;
            if (chunk.Count > 0 && length + encodedLength > maxEncodedLength)
            {
                yield return chunk.ToArray();
                chunk.Clear();
                length = 0;
            }

            chunk.Add(itemId);
            length += encodedLength;
        }

        if (chunk.Count > 0)
            yield return chunk.ToArray();
    }

    private static string BuildGoldUrl(string? server)
    {
        var baseUrl = AlbionDataConstants.GetBaseUrl(server).TrimEnd('/');
        return $"{baseUrl}/api/v2/stats/gold.json?count=1";
    }

    private static long ReadLong(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return 0;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
                return parsed;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                // Alguns feeds usam Unix epoch; outros podem vir em ticks. Tente ambos de forma segura.
                if (number > 621355968000000000)
                    return new DateTimeOffset(number, TimeSpan.Zero);

                if (number > 0)
                    return DateTimeOffset.FromUnixTimeSeconds(number);
            }
        }

        return null;
    }

    private static bool IsUsable(MarketPriceDto p)
    {
        if (string.IsNullOrWhiteSpace(p.ItemId) || string.IsNullOrWhiteSpace(p.city))
            return false;

        if (p.sell_price_min < 0 || p.sell_price_max < 0 || p.buy_price_min < 0 || p.buy_price_max < 0)
            return false;

        var hasSell = p.sell_price_min > 0 || p.sell_price_max > 0;
        var hasBuy = p.buy_price_min > 0 || p.buy_price_max > 0;
        if (!hasSell && !hasBuy)
            return false;

        if (p.sell_price_min > 0 && p.sell_price_max > 0 && p.sell_price_min > p.sell_price_max)
            return false;

        if (p.buy_price_min > 0 && p.buy_price_max > 0 && p.buy_price_min > p.buy_price_max)
            return false;

        return true;
    }
}
