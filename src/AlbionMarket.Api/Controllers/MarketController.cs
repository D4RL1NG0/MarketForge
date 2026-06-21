using Microsoft.AspNetCore.Mvc;
using AlbionMarket.Application.Features.Market.Interfaces;
using AlbionMarket.Application.Features.Market.GetPrices;
using AlbionMarket.Application.Features.Market.GetItems;
using AlbionMarket.Application.Features.Market.Catalog;
using AlbionMarket.Application.Features.Market.Cache;

namespace AlbionMarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly IMarketService _marketService;
    private readonly ItemCatalogService _catalog;
    private readonly IMarketPriceCacheStore _cacheStore;
    private readonly MarketCacheWarmupService _warmupService;

    public MarketController(IMarketService marketService, ItemCatalogService catalog, IMarketPriceCacheStore cacheStore, MarketCacheWarmupService warmupService)
    {
        _marketService = marketService;
        _catalog = catalog;
        _cacheStore = cacheStore;
        _warmupService = warmupService;
    }

    [HttpGet("prices")]
    public async Task<IActionResult> GetPrices([FromQuery] GetPricesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(new { error = "ItemId is required" });

        var result = await _marketService.GetPricesAsync(
            request.ItemId,
            request.City,
            request.Tier,
            request.Enchant,
            request.Quality,
            request.Server);

        return Ok(result);
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] GetItemsRequest request)
    {
        var result = await _marketService.GetItemsAsync(
            request.Search,
            request.Tier,
            request.Enchant,
            request.Category,
            request.SubCategory);

        return Ok(result);
    }

    [HttpGet("best-city")]
    public async Task<IActionResult> GetBestCity([FromQuery] string itemId, [FromQuery] int? tier = null, [FromQuery] int? enchant = null, [FromQuery] int? quality = null, [FromQuery] string? server = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return BadRequest(new { error = "ItemId is required" });

        var result = await _marketService.GetBestCityAsync(itemId, tier, enchant, quality, server);
        return Ok(result);
    }


    [HttpGet("gold")]
    public async Task<IActionResult> GetGold([FromQuery] string? server = null)
    {
        var result = await _marketService.GetLatestGoldAsync(server);
        return Ok(result);
    }


    [HttpGet("refining")]
    public async Task<IActionResult> GetRefining([FromQuery] string resource, [FromQuery] int tier = 5, [FromQuery] int enchant = 0, [FromQuery] string? server = null, [FromQuery] bool includeCaerleon = false)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return BadRequest(new { error = "resource is required" });

        var result = await _marketService.GetRefiningQuoteAsync(resource, tier, enchant, server, includeCaerleon);
        return Ok(result);
    }


    [HttpGet("crafting")]
    public async Task<IActionResult> GetCrafting([FromQuery] string itemId, [FromQuery] int tier = 4, [FromQuery] int enchant = 0, [FromQuery] int quality = 1, [FromQuery] string? server = null, [FromQuery] bool includeCaerleon = false)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return BadRequest(new { error = "itemId is required" });

        var result = await _marketService.GetCraftingQuoteAsync(itemId, tier, enchant, quality, server, includeCaerleon);
        return Ok(result);
    }


    [HttpGet("cache-status")]
    public IActionResult GetCacheStatus()
    {
        return Ok(new
        {
            persistentCacheEnabled = _cacheStore.IsEnabled,
            mode = _cacheStore.IsEnabled ? "neon-only-west" : "disabled",
            server = "west",
            maxVisibleAgeMinutes = 60,
            userRequestsHitAlbionData = false,
            message = _cacheStore.IsEnabled
                ? "Modo lançamento ativo: usuários leem somente Neon/Postgres no servidor West. Albion Data é chamada apenas pelo worker em segundo plano. Preços com mais de 60 minutos não são exibidos."
                : "Cache persistente desativado. Configure DATABASE_URL no Render para ativar Neon/Postgres."
        });
    }



    [HttpGet("cache/prewarm-status")]
    public IActionResult GetCachePrewarmStatus()
    {
        return Ok(_warmupService.GetStatus());
    }

    [HttpGet("cache/prewarm")]
    public async Task<IActionResult> PrewarmCache([FromQuery] string? server = "all", [FromQuery] string? scope = "core", [FromQuery] int? limit = null)
    {
        var result = await _warmupService.EnqueueNowAsync(server, scope, limit, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        var categories = _catalog.GetCategories();
        return Ok(categories);
    }

    [HttpGet("tiers")]
    public IActionResult GetAvailableTiers()
    {
        var tiers = _catalog.GetAvailableTiers();
        return Ok(tiers);
    }

    [HttpGet("enchantments")]
    public IActionResult GetAvailableEnchantments()
    {
        var enchantments = _catalog.GetAvailableEnchantments();
        return Ok(enchantments);
    }
}