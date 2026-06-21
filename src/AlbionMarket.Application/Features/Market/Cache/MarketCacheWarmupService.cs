using AlbionMarket.Application.Features.Market.Catalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AlbionMarket.Application.Features.Market.Cache;

public sealed class MarketCacheWarmupService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PeriodicDelay = TimeSpan.FromMinutes(15);
    private static readonly string[] DefaultServers = { "west" };
    private static readonly string[] RawResourceBases = { "WOOD", "ROCK", "ORE", "HIDE", "FIBER" };
    private static readonly string[] RefinedResourceBases = { "PLANKS", "STONEBLOCK", "METALBAR", "LEATHER", "CLOTH" };

    private readonly IMarketPriceCacheStore _store;
    private readonly ItemCatalogService _catalog;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusLock = new();

    private DateTimeOffset? _lastStartedAtUtc;
    private DateTimeOffset? _lastCompletedAtUtc;
    private string _lastScope = "none";
    private int _lastQueued;
    private string _lastMessage = "Prewarm ainda não executado.";

    public MarketCacheWarmupService(
        IMarketPriceCacheStore store,
        ItemCatalogService catalog,
        IConfiguration configuration)
    {
        _store = store;
        _catalog = catalog;
        _configuration = configuration;
    }

    public MarketCacheWarmupStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new MarketCacheWarmupStatus(
                _store.IsEnabled,
                _lastScope,
                _lastQueued,
                _lastStartedAtUtc,
                _lastCompletedAtUtc,
                _lastMessage);
        }
    }

    public async Task<MarketCacheWarmupResult> EnqueueNowAsync(
        string? server,
        string? scope,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        if (!_store.IsEnabled)
            return new MarketCacheWarmupResult(false, 0, "none", Array.Empty<string>(), "Cache persistente não está ativo. Configure DATABASE_URL primeiro.");

        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken))
            return new MarketCacheWarmupResult(false, 0, scope ?? "core", Array.Empty<string>(), "Prewarm já está rodando; aguarde terminar.");

        try
        {
            var normalizedScope = NormalizeScope(scope);
            var normalizedServers = ResolveServers(server);
            var itemLimit = ResolveLimit(limit, normalizedScope);
            var itemIds = BuildWarmupItemIds(normalizedScope, itemLimit);
            var started = DateTimeOffset.UtcNow;
            SetStatus(normalizedScope, 0, started, null, $"Enfileirando {itemIds.Count} itens para {string.Join(',', normalizedServers)}...");

            var queued = 0;
            foreach (var targetServer in normalizedServers)
            {
                // Qualidade 1 resolve refino/recursos e maioria dos itens. Qualidades 2-5 continuam sob demanda.
                foreach (var itemId in itemIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _store.EnqueuePriceSyncAsync(targetServer, itemId, quality: 1, priority: normalizedScope == "core" ? 50 : 10, cancellationToken);
                    queued++;
                }
            }

            var completed = DateTimeOffset.UtcNow;
            var message = $"Prewarm enfileirado. O worker vai popular o Neon aos poucos sem travar o app nem metralhar a Albion Data.";
            SetStatus(normalizedScope, queued, started, completed, message);
            return new MarketCacheWarmupResult(true, queued, normalizedScope, normalizedServers, message);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_store.IsEnabled)
        {
            Console.WriteLine("ℹ️ Persistent cache disabled. Market cache prewarm disabled.");
            return;
        }

        Console.WriteLine("✅ MarketForge cache prewarm scheduler started.");
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scope = _configuration["MARKETFORGE_PREWARM_SCOPE"] ?? "core";
                var server = "west";
                var limit = ReadInt(_configuration["MARKETFORGE_PREWARM_LIMIT"]);

                var result = await EnqueueNowAsync(server, scope, limit, stoppingToken);
                Console.WriteLine($"✅ Cache prewarm: {result.Message} Queued={result.Queued} Scope={result.Scope} Servers={string.Join(',', result.Servers)}");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Cache prewarm scheduler error: {ex.Message}");
            }

            await Task.Delay(PeriodicDelay, stoppingToken);
        }
    }

    private List<string> BuildWarmupItemIds(string scope, int limit)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in BuildCoreResourceItemIds())
            ids.Add(id);

        if (scope == "all")
        {
            foreach (var id in BuildCatalogMarketItemIds(limit))
                ids.Add(id);
        }

        return ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<string> BuildCatalogMarketItemIds(int limit)
    {
        // Usa a própria busca/categorias do app para montar uma lista ampla sem depender de API externa.
        // Limitamos para respeitar Render/Neon grátis e a Albion Data.
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categories = _catalog.GetCategories();

        foreach (var tier in Enumerable.Range(4, 5))
        {
            foreach (var enchant in Enumerable.Range(0, 4))
            {
                foreach (var category in categories)
                {
                    var groups = category.Groups.Count > 0
                        ? category.Groups.Select(x => x.Key)
                        : new[] { (string?)null };

                    foreach (var group in groups)
                    {
                        foreach (var item in _catalog.Search(string.Empty, $"T{tier}", enchant, limit: 150, category: category.Key, subCategory: group))
                        {
                            ids.Add(item.ItemId);
                            if (ids.Count >= limit)
                                return ids;
                        }
                    }
                }
            }
        }

        return ids;
    }

    private static IEnumerable<string> BuildCoreResourceItemIds()
    {
        for (var tier = 4; tier <= 8; tier++)
        {
            for (var enchant = 0; enchant <= 3; enchant++)
            {
                foreach (var raw in RawResourceBases)
                    yield return BuildResourceItemId(tier, raw, enchant);

                foreach (var refined in RefinedResourceBases)
                    yield return BuildResourceItemId(tier, refined, refined.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase) ? 0 : enchant);
            }

            // Itens muito usados nas telas e nos testes.
            yield return $"T{tier}_BAG";
            yield return $"T{tier}_CAPE";
        }
    }

    private static string BuildResourceItemId(int tier, string resourceBase, int enchant)
    {
        var id = $"T{tier}_{resourceBase}";
        if (enchant <= 0 || resourceBase.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase))
            return id;
        return $"{id}_LEVEL{enchant}@{enchant}";
    }

    private static string[] ResolveServers(string? server)
    {
        return DefaultServers;
    }

    private static string NormalizeServer(string? server)
    {
        return "west";
    }

    private static string NormalizeScope(string? scope)
    {
        // Lançamento inicial: só core. Ignora scope=all para não estourar rate limit.
        return "core";
    }

    private static int ResolveLimit(int? limit, string scope)
    {
        // Core atual tem cerca de 200 itens. Mantemos limite pequeno e seguro para Render/Neon grátis.
        return Math.Clamp(limit ?? 220, 50, 250);
    }

    private static int? ReadInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private void SetStatus(string scope, int queued, DateTimeOffset? started, DateTimeOffset? completed, string message)
    {
        lock (_statusLock)
        {
            _lastScope = scope;
            _lastQueued = queued;
            _lastStartedAtUtc = started;
            _lastCompletedAtUtc = completed;
            _lastMessage = message;
        }
    }
}

public sealed record MarketCacheWarmupResult(bool Accepted, int Queued, string Scope, IReadOnlyCollection<string> Servers, string Message);
public sealed record MarketCacheWarmupStatus(bool PersistentCacheEnabled, string LastScope, int LastQueued, DateTimeOffset? LastStartedAtUtc, DateTimeOffset? LastCompletedAtUtc, string Message);
