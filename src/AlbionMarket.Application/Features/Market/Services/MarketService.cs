using System.Collections.Concurrent;
using System.Threading;
using AlbionMarket.Application.Features.Market.Catalog;
using AlbionMarket.Application.Features.Market.Cache;
using AlbionMarket.Application.Features.Market.Common;
using AlbionMarket.Application.Features.Market.GetItems;
using AlbionMarket.Application.Features.Market.Interfaces;
using AlbionMarket.Application.Features.Market.Models;
using AlbionMarket.Infrastructure.AlbionDataApi.Clients;
using AlbionMarket.Infrastructure.AlbionDataApi.Models;

namespace AlbionMarket.Application.Features.Market.Services;

public class MarketService : IMarketService
{
    private const int DefaultQuality = 1;
    private static readonly ConcurrentDictionary<string, MarketPrice> LastValidPrices = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, PriceCacheEntry> PriceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<Task<List<MarketPrice>>>> PriceRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, GoldCacheEntry> GoldCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan NegativePriceCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IAlbionDataClient _client;
    private readonly ItemCatalogService _catalog;
    private readonly IMarketPriceCacheStore _persistentCache;

    public MarketService(IAlbionDataClient client, ItemCatalogService catalog, IMarketPriceCacheStore persistentCache)
    {
        _client = client;
        _catalog = catalog;
        _persistentCache = persistentCache;
    }

    public async Task<List<MarketPrice>> GetPricesAsync(string itemId, string? city = null, int? tier = null, int? enchant = null, int? quality = null, string? server = null)
    {
        var catalogItem = _catalog.ResolveCatalogItem(itemId, tier, enchant);
        var resolvedItemId = catalogItem?.ItemId ?? string.Empty;
        var itemName = catalogItem?.Name ?? itemId;
        var itemNamePtBr = catalogItem?.NamePtBr ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedItemId))
            return new List<MarketPrice>();

        var locations = string.IsNullOrWhiteSpace(city)
            ? Cities.All
            : new[] { city.Trim() };

        // Quality NÃO é encantamento. Encantamento é parte do itemId: T4_BAG@1.
        // Quality vem do filtro do usuário: 1 normal, 2 boa, 3 notável, 4 excelente, 5 obra-prima.
        var selectedQuality = quality is >= 1 and <= 5 ? quality.Value : DefaultQuality;
        var locationArray = locations.ToArray();
        var serverKey = NormalizeServer(server);
        var cacheKey = BuildPriceCacheKey(resolvedItemId, locationArray, selectedQuality, serverKey);
        var now = DateTimeOffset.UtcNow;
        if (PriceCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > now)
            return cached.Prices.Select(ClonePrice).ToList();

        if (_persistentCache.IsEnabled)
        {
            var persistentFresh = await _persistentCache.GetPricesAsync(serverKey, resolvedItemId, selectedQuality, locationArray, allowStale: false);
            if (persistentFresh.Any())
            {
                var named = ApplyNames(persistentFresh, itemName, itemNamePtBr);
                PriceCache[cacheKey] = new PriceCacheEntry(named.Select(ClonePrice).ToList(), NextAlbionPriceRefreshWindowUtc());
                return named.Select(ClonePrice).ToList();
            }

            // Modo lançamento: request de usuário NUNCA chama Albion Data.
            // Se existir preço no Neon com até 60 minutos, mostra e agenda atualização.
            var persistentStale = await _persistentCache.GetPricesAsync(serverKey, resolvedItemId, selectedQuality, locationArray, allowStale: true);
            if (persistentStale.Any())
            {
                await _persistentCache.EnqueuePriceSyncAsync(serverKey, resolvedItemId, selectedQuality, priority: 5);
                var named = ApplyNames(persistentStale, itemName, itemNamePtBr);
                PriceCache[cacheKey] = new PriceCacheEntry(named.Select(ClonePrice).ToList(), DateTimeOffset.UtcNow.AddSeconds(30));
                return named.Select(ClonePrice).ToList();
            }

            // Sem preço recente no Neon: apenas enfileira. O app deve mostrar "sincronizando"/vazio.
            await _persistentCache.EnqueuePriceSyncAsync(serverKey, resolvedItemId, selectedQuality, priority: 10);
            PriceCache[cacheKey] = new PriceCacheEntry(new List<MarketPrice>(), DateTimeOffset.UtcNow.AddSeconds(10));
            return new List<MarketPrice>();
        }

        // Sem DATABASE_URL não usamos Albion Data em request de usuário.
        // Isso evita lançar uma versão que metralha a API externa por engano.
        PriceCache[cacheKey] = new PriceCacheEntry(new List<MarketPrice>(), DateTimeOffset.UtcNow.AddSeconds(10));
        return new List<MarketPrice>();
    }

    private async Task<List<MarketPrice>> FetchAndCachePricesAsync(
        string resolvedItemId,
        IReadOnlyCollection<string> locations,
        int selectedQuality,
        string? server,
        string itemName,
        string itemNamePtBr,
        string cacheKey)
    {
        var result = await _client.GetPricesAsync(resolvedItemId, locations, selectedQuality, server);

        if (!result.Any())
        {
            // Cache negativo curto: evita martelar a Albion Data API quando ela responde 429
            // ou quando um item/cidade está temporariamente sem preço.
            var negativeDuration = _persistentCache.IsEnabled ? TimeSpan.FromSeconds(8) : NegativePriceCacheDuration;
            PriceCache[cacheKey] = new PriceCacheEntry(new List<MarketPrice>(), DateTimeOffset.UtcNow.Add(negativeDuration));
            if (_persistentCache.IsEnabled)
                await _persistentCache.EnqueuePriceSyncAsync(NormalizeServer(server), resolvedItemId, selectedQuality, priority: 1);
            return new List<MarketPrice>();
        }

        var normalized = result
            .Where(IsValidPrice)
            .GroupBy(x => $"{x.ItemId}|{x.city}", StringComparer.OrdinalIgnoreCase)
            .Select(g => PickBestSnapshot(g))
            .Select(x => ToMarketPrice(x, itemName, itemNamePtBr))
            .Where(x => HasAnyUsablePrice(x))
            .ToList();

        var cleaned = CleanAndStabilizePrices(normalized);
        var validUntil = NextAlbionPriceRefreshWindowUtc();
        PriceCache[cacheKey] = new PriceCacheEntry(cleaned.Select(ClonePrice).ToList(), validUntil);
        if (_persistentCache.IsEnabled)
        {
            var normalizedServer = NormalizeServer(server);
            await _persistentCache.UpsertPricesAsync(normalizedServer, resolvedItemId, selectedQuality, cleaned, validUntil);

            // Se esse item já estava na fila, removemos depois que a consulta ao vivo conseguiu
            // salvar o preço. Sem isso, o worker busca o mesmo item logo em seguida e duplica
            // request externa.
            await _persistentCache.CompletePriceSyncAsync(new[]
            {
                new PriceSyncRequest(normalizedServer, resolvedItemId, selectedQuality, 0)
            });
        }

        return cleaned;
    }

    public Task<List<GetItemsResponse>> GetItemsAsync(string? search, string? tier, int? enchant, string? category = null, string? subCategory = null)
    {
        var items = _catalog.Search(search ?? string.Empty, tier, enchant, category: category, subCategory: subCategory);

        return Task.FromResult(items.Select(x => new GetItemsResponse
        {
            ItemId = x.ItemId,
            Name = x.Name,
            NamePtBr = x.NamePtBr,
            Tier = x.Tier,
            Enchantment = x.Enchantment,
            Category = x.Category,
            SubCategory = x.SubCategory
        }).ToList());
    }

    public async Task<ItemMarketOpportunity> GetBestCityAsync(string itemId, int? tier = null, int? enchant = null, int? quality = null, string? server = null)
    {
        var catalogItem = _catalog.ResolveCatalogItem(itemId, tier, enchant);
        var resolvedItemId = catalogItem?.ItemId ?? string.Empty;
        var itemName = catalogItem?.Name ?? itemId;
        var itemNamePtBr = catalogItem?.NamePtBr ?? string.Empty;
        var prices = await GetPricesAsync(resolvedItemId, null, tier, enchant, quality, server);

        if (!prices.Any())
        {
            return NoOpportunity(resolvedItemId, itemName, itemNamePtBr, "Nenhum preço válido encontrado para este item.");
        }

        // Melhor compra: cidade com o menor preço de venda atual (sell_price_min),
        // ou seja, onde o jogador paga menos para comprar o item.
        var sellCandidates = prices
            .Where(x => x.SellPriceMin > 0)
            .GroupBy(x => x.City, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.SellPriceMin).First())
            .ToList();

        var bestBuy = sellCandidates
            .OrderBy(x => x.SellPriceMin)
            .FirstOrDefault();

        if (bestBuy is null)
            return NoOpportunity(resolvedItemId, itemName, itemNamePtBr, "Nenhuma cidade com preço de compra válido.");

        // Melhor venda: maior sell_price_min em cidade diferente da compra.
        // Mesmo sem lucro, retornamos a melhor compra/venda para a UI mostrar os extremos de preço.
        var bestSell = sellCandidates
            .Where(x => !string.Equals(x.City, bestBuy.City, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.SellPriceMin)
            .FirstOrDefault();

        if (bestSell is null)
            return NoOpportunity(resolvedItemId, itemName, itemNamePtBr, "Só existe preço válido em uma cidade.", bestBuy.City, bestBuy.SellPriceMin);

        var opportunity = new ItemMarketOpportunity
        {
            ItemId = resolvedItemId,
            ItemName = itemName,
            ItemNamePtBr = itemNamePtBr,
            BestBuyCity = bestBuy.City,
            BestBuyPrice = bestBuy.SellPriceMin,
            BestSellCity = bestSell.City,
            BestSellPrice = bestSell.SellPriceMin
        };

        opportunity.HasOpportunity = opportunity.ProfitWithPremium > 0;
        opportunity.Message = opportunity.HasOpportunity
            ? "Oportunidade líquida encontrada com premium."
            : opportunity.Profit > 0
                ? "Existe diferença bruta, mas a taxa pode remover o lucro líquido."
                : "Melhores preços encontrados, mas sem lucro positivo entre cidades diferentes.";

        return opportunity;
    }

    public async Task<GoldPrice> GetLatestGoldAsync(string? server = null)
    {
        var serverKey = NormalizeServer(server);
        var now = DateTimeOffset.UtcNow;
        if (GoldCache.TryGetValue(serverKey, out var cached) && cached.ExpiresAt > now)
            return CloneGold(cached.Gold);

        if (_persistentCache.IsEnabled)
        {
            var cachedGold = await _persistentCache.GetGoldAsync(serverKey, allowStale: false);
            if (cachedGold is not null && cachedGold.Price > 0)
            {
                GoldCache[serverKey] = new GoldCacheEntry(CloneGold(cachedGold), NextAlbionPriceRefreshWindowUtc());
                return cachedGold;
            }

            var staleGold = await _persistentCache.GetGoldAsync(serverKey, allowStale: true);
            if (staleGold is not null && staleGold.Price > 0)
            {
                GoldCache[serverKey] = new GoldCacheEntry(CloneGold(staleGold), DateTimeOffset.UtcNow.AddSeconds(30));
                return staleGold;
            }
        }

        return new GoldPrice
        {
            Server = serverKey,
            Price = 0,
            UpdatedAtUtc = null
        };
    }


    public async Task<RefiningQuote> GetRefiningQuoteAsync(string resource, int tier, int enchant, string? server = null, bool includeCaerleon = false)
    {
        var recipeInfo = RefiningResourceInfo.From(resource);
        tier = Math.Clamp(tier, 4, 8);
        enchant = Math.Clamp(enchant, 0, 4);
        var effectiveProductEnchant = recipeInfo.RefinedBase.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase) ? 0 : enchant;
        var serverKey = NormalizeServer(server);

        var quote = new RefiningQuote
        {
            Resource = recipeInfo.Key,
            ResourceNamePtBr = recipeInfo.NamePtBr,
            ResourceNameEn = recipeInfo.NameEn,
            Tier = tier,
            Enchantment = enchant,
            Server = serverKey,
            RefinedItemId = BuildResourceItemId(tier, recipeInfo.RefinedBase, effectiveProductEnchant),
            ValidUntilUtc = NextAlbionPriceRefreshWindowUtc()
        };

        var refinedCatalog = _catalog.ResolveCatalogItem(quote.RefinedItemId, tier, effectiveProductEnchant);
        quote.RefinedItemName = refinedCatalog?.Name ?? quote.RefinedItemId;
        quote.RefinedItemNamePtBr = refinedCatalog?.NamePtBr ?? string.Empty;

        // Receita real do Albion: pega SOMENTE os ingredientes diretos do item refinado no items.xml.
        // Exemplo: T6_LEATHER => T6_HIDE + T5_LEATHER. Não monta cadeia recursiva.
        var recipeEnchant = recipeInfo.RefinedBase.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase) ? enchant : effectiveProductEnchant;
        var recipes = _catalog.GetCraftingRecipeOptions(quote.RefinedItemId, tier, recipeEnchant);
        if (recipes.Count == 0)
        {
            quote.Message = $"Receita real não encontrada no items.xml para {quote.RefinedItemId}.";
            return quote;
        }

        var primaryRecipes = recipes
            .Where(x => !x.Ingredients.Any(IsFactionTokenIngredient))
            .ToList();
        if (primaryRecipes.Count == 0)
            primaryRecipes = recipes.ToList();

        var refiningBonus = GetRefiningProductionBonus(recipeInfo.Key);
        quote.RefiningCity = refiningBonus.City;
        quote.ResourceReturnRate = refiningBonus.ReturnRate;

        var quotedRecipes = new List<RefiningRecipeCost>();
        foreach (var recipe in primaryRecipes)
        {
            var quotedRecipe = await BuildRefiningRecipeCostAsync(recipe, server, refiningBonus.ReturnRate, includeCaerleon);
            if (quotedRecipe.IsComplete)
                quotedRecipes.Add(quotedRecipe);
        }

        var bestRecipe = quotedRecipes
            .OrderBy(x => x.TotalCost)
            .FirstOrDefault();

        if (bestRecipe is null)
        {
            quote.Message = _persistentCache.IsEnabled ? "Preços enfileirados para sincronização. Aguarde alguns segundos e tente novamente." : "Sem preço válido para um ou mais ingredientes diretos do refino.";
            return quote;
        }

        quote.Ingredients = bestRecipe.Ingredients;
        quote.GrossIngredientCost = bestRecipe.GrossTotalCost;
        quote.EffectiveIngredientCost = bestRecipe.TotalCost;
        quote.TotalIngredientCost = bestRecipe.TotalCost;

        var refinedPrices = await GetPricesAsync(quote.RefinedItemId, null, tier, effectiveProductEnchant, DefaultQuality, server);
        var bestSell = FilterCaerleon(refinedPrices, includeCaerleon)
            .Where(x => x.SellPriceMin > 0)
            .OrderByDescending(x => x.SellPriceMin)
            .FirstOrDefault();

        if (bestSell is null)
        {
            quote.Message = _persistentCache.IsEnabled ? $"Preço de venda de {quote.RefinedItemId} enfileirado para sincronização. Aguarde alguns segundos e tente novamente." : $"Sem preço válido para vender {quote.RefinedItemId}.";
            quote.UpdatedAtUtc = quote.Ingredients.Select(x => x.UpdatedAtUtc).Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty().Max();
            return quote;
        }

        var outputQuantity = Math.Max(1, bestRecipe.OutputQuantity);
        var grossSellValue = bestSell.SellPriceMin * outputQuantity;
        quote.BestSellCity = bestSell.City;
        quote.BestSellPrice = bestSell.SellPriceMin;
        quote.NetSellWithPremium = ApplySaleTax(grossSellValue, 0.04m);
        quote.NetSellWithoutPremium = ApplySaleTax(grossSellValue, 0.08m);
        quote.SaleTaxWithPremium = Math.Max(0, grossSellValue - quote.NetSellWithPremium);
        quote.SaleTaxWithoutPremium = Math.Max(0, grossSellValue - quote.NetSellWithoutPremium);
        quote.ProfitWithPremium = quote.NetSellWithPremium - quote.TotalIngredientCost;
        quote.ProfitWithoutPremium = quote.NetSellWithoutPremium - quote.TotalIngredientCost;
        quote.ProfitPercentWithPremium = CalculateProfitPercent(quote.ProfitWithPremium, grossSellValue);
        quote.ProfitPercentWithoutPremium = CalculateProfitPercent(quote.ProfitWithoutPremium, grossSellValue);
        quote.HasOpportunityWithPremium = quote.ProfitWithPremium > 0;
        quote.HasOpportunityWithoutPremium = quote.ProfitWithoutPremium > 0;

        var dates = quote.Ingredients.Select(x => x.UpdatedAtUtc).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (bestSell.UpdatedAtUtc.HasValue)
            dates.Add(bestSell.UpdatedAtUtc.Value);
        quote.UpdatedAtUtc = dates.Count > 0 ? dates.Max() : null;

        quote.Message = quote.HasOpportunityWithPremium
            ? $"Refino com lucro líquido encontrado. Bônus aplicado em {quote.RefiningCity}."
            : $"Sem lucro líquido nos melhores preços atuais. Bônus aplicado em {quote.RefiningCity}.";

        return quote;
    }

    public async Task<CraftingQuote> GetCraftingQuoteAsync(string itemId, int tier, int enchant, int quality, string? server = null, bool includeCaerleon = false)
    {
        tier = Math.Clamp(tier, 4, 8);
        enchant = Math.Clamp(enchant, 0, 4);
        quality = quality is >= 1 and <= 5 ? quality : DefaultQuality;
        var serverKey = NormalizeServer(server);
        var product = _catalog.ResolveCatalogItem(itemId, tier, enchant);
        var resolvedItemId = product?.ItemId ?? string.Empty;

        var quote = new CraftingQuote
        {
            ItemId = resolvedItemId,
            ItemName = product?.Name ?? itemId,
            ItemNamePtBr = product?.NamePtBr ?? string.Empty,
            Tier = tier,
            Enchantment = enchant,
            Quality = quality,
            Server = serverKey,
            ValidUntilUtc = NextAlbionPriceRefreshWindowUtc()
        };

        if (string.IsNullOrWhiteSpace(resolvedItemId))
        {
            quote.Message = "Item inválido para craft.";
            return quote;
        }

        var recipes = _catalog.GetCraftingRecipeOptions(resolvedItemId, tier, enchant);
        if (recipes.Count == 0)
        {
            quote.Message = "Este item não possui receita de craft no catálogo local.";
            return quote;
        }

        var craftingBonus = GetCraftingProductionBonus(product);
        quote.CraftingCity = craftingBonus.City;
        quote.ResourceReturnRate = craftingBonus.ReturnRate;

        var quotedRecipes = new List<CraftingRecipeCost>();
        foreach (var recipe in recipes)
        {
            var quotedRecipe = await BuildCraftingRecipeCostAsync(recipe, server, craftingBonus.ReturnRate, includeCaerleon);
            if (quotedRecipe.IsComplete)
                quotedRecipes.Add(quotedRecipe);
        }

        var bestRecipe = quotedRecipes
            .OrderBy(x => x.TotalCost)
            .FirstOrDefault();

        if (bestRecipe is null)
        {
            quote.Message = _persistentCache.IsEnabled ? "Preços de ingredientes enfileirados para sincronização. Aguarde alguns segundos e tente novamente." : "Sem preço válido para um ou mais ingredientes de craft.";
            return quote;
        }

        quote.Ingredients = bestRecipe.Ingredients;
        quote.GrossIngredientCost = bestRecipe.GrossTotalCost;
        quote.EffectiveIngredientCost = bestRecipe.TotalCost;
        quote.TotalIngredientCost = bestRecipe.TotalCost;

        var productPrices = await GetPricesAsync(resolvedItemId, null, tier, enchant, quality, server);
        var bestSell = FilterCaerleon(productPrices, includeCaerleon)
            .Where(x => x.SellPriceMin > 0)
            .OrderByDescending(x => x.SellPriceMin)
            .FirstOrDefault();

        if (bestSell is null)
        {
            quote.Message = _persistentCache.IsEnabled ? $"Preço de venda de {resolvedItemId} enfileirado para sincronização. Aguarde alguns segundos e tente novamente." : $"Sem preço válido para vender {resolvedItemId}.";
            quote.UpdatedAtUtc = quote.Ingredients.Select(x => x.UpdatedAtUtc).Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty().Max();
            return quote;
        }

        var outputQuantity = Math.Max(1, bestRecipe.OutputQuantity);
        var grossSellValue = bestSell.SellPriceMin * outputQuantity;
        quote.BestSellCity = bestSell.City;
        quote.BestSellPrice = bestSell.SellPriceMin;
        quote.NetSellWithPremium = ApplySaleTax(grossSellValue, 0.04m);
        quote.NetSellWithoutPremium = ApplySaleTax(grossSellValue, 0.08m);
        quote.SaleTaxWithPremium = Math.Max(0, grossSellValue - quote.NetSellWithPremium);
        quote.SaleTaxWithoutPremium = Math.Max(0, grossSellValue - quote.NetSellWithoutPremium);
        quote.ProfitWithPremium = quote.NetSellWithPremium - quote.TotalIngredientCost;
        quote.ProfitWithoutPremium = quote.NetSellWithoutPremium - quote.TotalIngredientCost;
        quote.ProfitPercentWithPremium = CalculateProfitPercent(quote.ProfitWithPremium, grossSellValue);
        quote.ProfitPercentWithoutPremium = CalculateProfitPercent(quote.ProfitWithoutPremium, grossSellValue);
        quote.HasOpportunityWithPremium = quote.ProfitWithPremium > 0;
        quote.HasOpportunityWithoutPremium = quote.ProfitWithoutPremium > 0;

        var dates = quote.Ingredients.Select(x => x.UpdatedAtUtc).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (bestSell.UpdatedAtUtc.HasValue)
            dates.Add(bestSell.UpdatedAtUtc.Value);
        quote.UpdatedAtUtc = dates.Count > 0 ? dates.Max() : null;
        quote.Message = quote.HasOpportunityWithPremium
            ? "Craft com lucro líquido encontrado."
            : "Sem lucro líquido nos melhores preços atuais.";

        return quote;
    }

    private async Task<CraftingRecipeCost> BuildCraftingRecipeCostAsync(CraftingRecipeOption recipe, string? server, decimal resourceReturnRate, bool includeCaerleon)
    {
        var result = new List<CraftingIngredientCost>();
        long grossTotal = 0;
        long effectiveTotal = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            var ingredientEnchant = ExtractEnchantFromItemId(ingredient.ItemId);
            var ingredientTier = ExtractTierFromItemId(ingredient.ItemId);
            var catalog = _catalog.ResolveCatalogItem(ingredient.ItemId, ingredientTier, ingredientEnchant);
            var prices = await GetPricesAsync(ingredient.ItemId, null, ingredientTier, ingredientEnchant, DefaultQuality, server);
            var cheapest = FilterCaerleon(prices, includeCaerleon)
                .Where(x => x.SellPriceMin > 0)
                .OrderBy(x => x.SellPriceMin)
                .FirstOrDefault();

            if (cheapest is null)
                return new CraftingRecipeCost(false, new List<CraftingIngredientCost>(), 0, 0, recipe.OutputQuantity);

            var gross = cheapest.SellPriceMin * ingredient.Quantity;
            var effective = ingredient.IsReturnable ? ApplyResourceReturnRate(gross, resourceReturnRate) : gross;
            grossTotal += gross;
            effectiveTotal += effective;

            result.Add(new CraftingIngredientCost
            {
                ItemId = ingredient.ItemId,
                ItemName = catalog?.Name ?? ingredient.ItemId,
                ItemNamePtBr = catalog?.NamePtBr ?? string.Empty,
                Quantity = ingredient.Quantity,
                City = cheapest.City,
                UnitPrice = cheapest.SellPriceMin,
                TotalPrice = effective,
                UpdatedAtUtc = cheapest.UpdatedAtUtc
            });
        }

        return new CraftingRecipeCost(true, result, effectiveTotal, grossTotal, recipe.OutputQuantity);
    }

    private async Task<RefiningRecipeCost> BuildRefiningRecipeCostAsync(CraftingRecipeOption recipe, string? server, decimal resourceReturnRate, bool includeCaerleon)
    {
        var result = new List<RefiningIngredientCost>();
        long grossTotal = 0;
        long effectiveTotal = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            var ingredientEnchant = ExtractEnchantFromItemId(ingredient.ItemId);
            var ingredientTier = ExtractTierFromItemId(ingredient.ItemId);
            var catalog = _catalog.ResolveCatalogItem(ingredient.ItemId, ingredientTier, ingredientEnchant);
            var prices = await GetPricesAsync(ingredient.ItemId, null, ingredientTier, ingredientEnchant, DefaultQuality, server);
            var cheapest = FilterCaerleon(prices, includeCaerleon)
                .Where(x => x.SellPriceMin > 0)
                .OrderBy(x => x.SellPriceMin)
                .FirstOrDefault();

            if (cheapest is null)
                return new RefiningRecipeCost(false, new List<RefiningIngredientCost>(), 0, 0, recipe.OutputQuantity);

            var gross = cheapest.SellPriceMin * ingredient.Quantity;
            var effective = ingredient.IsReturnable ? ApplyResourceReturnRate(gross, resourceReturnRate) : gross;
            grossTotal += gross;
            effectiveTotal += effective;

            result.Add(new RefiningIngredientCost
            {
                ItemId = ingredient.ItemId,
                ItemName = catalog?.Name ?? ingredient.ItemId,
                ItemNamePtBr = catalog?.NamePtBr ?? string.Empty,
                Tier = ingredientTier,
                Enchantment = ingredientEnchant,
                Quantity = ingredient.Quantity,
                City = cheapest.City,
                UnitPrice = cheapest.SellPriceMin,
                TotalPrice = effective,
                UpdatedAtUtc = cheapest.UpdatedAtUtc
            });
        }

        return new RefiningRecipeCost(true, result, effectiveTotal, grossTotal, recipe.OutputQuantity);
    }

    private static ItemMarketOpportunity NoOpportunity(string itemId, string itemName, string itemNamePtBr, string message, string? bestBuyCity = null, long bestBuyPrice = 0)
    {
        return new ItemMarketOpportunity
        {
            ItemId = itemId,
            ItemName = itemName,
            ItemNamePtBr = itemNamePtBr,
            BestBuyCity = bestBuyCity ?? "N/A",
            BestBuyPrice = bestBuyPrice,
            BestSellCity = "N/A",
            BestSellPrice = 0,
            HasOpportunity = false,
            Message = message
        };
    }

    private static MarketPriceDto PickBestSnapshot(IEnumerable<MarketPriceDto> prices)
    {
        return prices
            .OrderByDescending(x => x.sell_price_min > 0 || x.buy_price_max > 0)
            .ThenByDescending(x => x.sell_volume + x.buy_volume)
            .ThenByDescending(x => Math.Max(x.sell_price_min, x.buy_price_max))
            .First();
    }

    private static DateTimeOffset? LatestDate(params DateTimeOffset?[] values)
    {
        var valid = values
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderByDescending(x => x)
            .ToList();

        return valid.Count == 0 ? null : valid[0];
    }

    private static MarketPrice ToMarketPrice(MarketPriceDto x, string itemName, string itemNamePtBr)
    {
        return new MarketPrice
        {
            ItemId = x.ItemId,
            ItemName = itemName,
            ItemNamePtBr = itemNamePtBr,
            City = x.city,
            SellPriceMin = x.sell_price_min,
            SellPriceMax = x.sell_price_max,
            BuyPriceMin = x.buy_price_min,
            BuyPriceMax = x.buy_price_max,
            UpdatedAtUtc = LatestDate(x.sell_price_min_date, x.sell_price_max_date, x.buy_price_min_date, x.buy_price_max_date)
        };
    }

    private static List<MarketPrice> CleanAndStabilizePrices(List<MarketPrice> prices)
    {
        if (prices.Count == 0)
            return prices;

        var sellMedian = Median(prices.Select(x => x.SellPriceMin).Where(x => x > 0));
        var buyMedian = Median(prices.Select(x => x.BuyPriceMax).Where(x => x > 0));

        var cleaned = prices
            .Select(price => Stabilize(price, sellMedian, buyMedian))
            .Where(HasAnyUsablePrice)
            .GroupBy(x => $"{x.ItemId}|{x.City}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.City)
            .ToList();

        foreach (var price in cleaned)
            LastValidPrices[Key(price)] = price;

        return cleaned;
    }

    private static MarketPrice Stabilize(MarketPrice price, decimal sellMedian, decimal buyMedian)
    {
        var key = Key(price);
        LastValidPrices.TryGetValue(key, out var lastValid);

        if (IsOutlier(price.SellPriceMin, sellMedian))
            price.SellPriceMin = lastValid?.SellPriceMin > 0 ? lastValid.SellPriceMin : 0;

        if (IsOutlier(price.SellPriceMax, sellMedian))
            price.SellPriceMax = lastValid?.SellPriceMax > 0 ? lastValid.SellPriceMax : 0;

        if (IsOutlier(price.BuyPriceMax, buyMedian))
            price.BuyPriceMax = lastValid?.BuyPriceMax > 0 ? lastValid.BuyPriceMax : 0;

        if (IsOutlier(price.BuyPriceMin, buyMedian))
            price.BuyPriceMin = lastValid?.BuyPriceMin > 0 ? lastValid.BuyPriceMin : 0;

        return price;
    }

    private static bool IsOutlier(long value, decimal median)
    {
        if (value <= 0 || median <= 0)
            return false;

        return value > median * 10 || value < median * 0.10m;
    }

    private static decimal Median(IEnumerable<long> values)
    {
        var ordered = values.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
            return 0;

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
    }

    private static bool IsValidPrice(MarketPriceDto price)
    {
        if (string.IsNullOrWhiteSpace(price.ItemId) || string.IsNullOrWhiteSpace(price.city))
            return false;

        if (price.sell_price_min < 0 || price.sell_price_max < 0 || price.buy_price_min < 0 || price.buy_price_max < 0)
            return false;

        // A API às vezes retorna um lado do range zerado. Não jogue fora uma cidade só por isso.
        var hasSell = price.sell_price_min > 0 || price.sell_price_max > 0;
        var hasBuy = price.buy_price_min > 0 || price.buy_price_max > 0;
        if (!hasSell && !hasBuy)
            return false;

        if (price.sell_price_min > 0 && price.sell_price_max > 0 && price.sell_price_min > price.sell_price_max)
            return false;

        if (price.buy_price_min > 0 && price.buy_price_max > 0 && price.buy_price_min > price.buy_price_max)
            return false;

        return true;
    }

    private static bool HasAnyUsablePrice(MarketPrice price)
    {
        return price.SellPriceMin > 0 || price.SellPriceMax > 0 || price.BuyPriceMin > 0 || price.BuyPriceMax > 0;
    }

    private static List<MarketPrice> ApplyNames(IEnumerable<MarketPrice> prices, string itemName, string itemNamePtBr)
    {
        return prices.Select(price =>
        {
            var clone = ClonePrice(price);
            clone.ItemName = itemName;
            clone.ItemNamePtBr = itemNamePtBr;
            return clone;
        }).ToList();
    }

    private static string NormalizeServer(string? server)
    {
        // Lançamento inicial: foco 100% West. Ignora qualquer parâmetro de servidor.
        return "west";
    }

    private static string BuildPriceCacheKey(string itemId, IEnumerable<string> locations, int quality, string? server)
    {
        var locationsKey = string.Join(',', locations.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var serverKey = NormalizeServer(server);
        return $"{serverKey}|{itemId.ToUpperInvariant()}|{quality}|{locationsKey}";
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

    private static MarketPrice ClonePrice(MarketPrice price)
    {
        return new MarketPrice
        {
            ItemId = price.ItemId,
            ItemName = price.ItemName,
            ItemNamePtBr = price.ItemNamePtBr,
            City = price.City,
            SellPriceMin = price.SellPriceMin,
            SellPriceMax = price.SellPriceMax,
            BuyPriceMin = price.BuyPriceMin,
            BuyPriceMax = price.BuyPriceMax,
            UpdatedAtUtc = price.UpdatedAtUtc
        };
    }

    private static GoldPrice CloneGold(GoldPrice gold)
    {
        return new GoldPrice
        {
            Server = gold.Server,
            Price = gold.Price,
            UpdatedAtUtc = gold.UpdatedAtUtc
        };
    }

    private static long ApplyResourceReturnRate(long grossCost, decimal returnRate)
    {
        if (grossCost <= 0 || returnRate <= 0)
            return grossCost;

        return (long)Math.Ceiling(grossCost * (1m - Math.Clamp(returnRate, 0m, 0.95m)));
    }

    private static bool IsFactionTokenIngredient(CraftingRecipeIngredient ingredient)
    {
        return ingredient.ItemId.Contains("FACTION", StringComparison.OrdinalIgnoreCase)
            || ingredient.ItemId.Contains("TOKEN", StringComparison.OrdinalIgnoreCase);
    }

    private static ProductionBonus GetRefiningProductionBonus(string resourceKey)
    {
        // RRR aproximado de cidade real sem foco: as cidades reais têm bônus local de refino.
        // A taxa exata pode variar com foco/premium/estação, então usamos a base mais comum sem foco para sniper rápido.
        return resourceKey.Trim().ToLowerInvariant() switch
        {
            "wood" => new ProductionBonus("Fort Sterling", 0.367m),
            "stone" => new ProductionBonus("Bridgewatch", 0.367m),
            "ore" => new ProductionBonus("Thetford", 0.367m),
            "hide" => new ProductionBonus("Martlock", 0.367m),
            _ => new ProductionBonus("Cidade com bônus", 0.367m)
        };
    }

    private static ProductionBonus GetCraftingProductionBonus(ItemCatalogItem? item)
    {
        // Craft tem bônus local por família de item e varia bastante por categoria.
        // Até mapearmos cada família 1:1, mantemos seguro: sem bônus inventado.
        return new ProductionBonus("Sem bônus específico", 0m);
    }

    private sealed record ProductionBonus(string City, decimal ReturnRate);
    private sealed record PriceCacheEntry(List<MarketPrice> Prices, DateTimeOffset ExpiresAt);
    private sealed record GoldCacheEntry(GoldPrice Gold, DateTimeOffset ExpiresAt);

    private sealed record CraftingRecipeCost(bool IsComplete, List<CraftingIngredientCost> Ingredients, long TotalCost, long GrossTotalCost, int OutputQuantity);
    private sealed record RefiningRecipeCost(bool IsComplete, List<RefiningIngredientCost> Ingredients, long TotalCost, long GrossTotalCost, int OutputQuantity);
    private sealed record RefiningMaterialRequirement(string ItemId, int Tier, int Enchantment, int Quantity);

    private static int ExtractEnchantFromItemId(string itemId)
    {
        var index = itemId.IndexOf('@');
        if (index < 0 || index == itemId.Length - 1)
            return 0;
        return int.TryParse(itemId[(index + 1)..], out var enchant) ? Math.Clamp(enchant, 0, 4) : 0;
    }

    private static int ExtractTierFromItemId(string itemId)
    {
        if (itemId.Length >= 2 && itemId[0] is 'T' or 't' && char.IsDigit(itemId[1]))
            return itemId[1] - '0';
        return 4;
    }

    private static IEnumerable<MarketPrice> FilterCaerleon(IEnumerable<MarketPrice> prices, bool includeCaerleon)
    {
        return includeCaerleon
            ? prices
            : prices.Where(x => !string.Equals(x.City, "Caerleon", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal CalculateProfitPercent(long profit, long grossSellValue)
    {
        if (grossSellValue <= 0)
            return 0m;

        return Math.Round((decimal)profit / grossSellValue * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static long ApplySaleTax(long sellPrice, decimal taxRate)
    {
        if (sellPrice <= 0)
            return 0;
        return (long)Math.Floor(sellPrice * (1m - taxRate));
    }

    private static List<RefiningMaterialRequirement> BuildRefiningRequirements(int tier, int enchant, RefiningResourceInfo recipe)
    {
        var requirements = new List<RefiningMaterialRequirement>();
        var rawEnchant = tier >= 4 ? enchant : 0;
        var rawId = BuildResourceItemId(tier, recipe.RawBase, rawEnchant);
        requirements.Add(new RefiningMaterialRequirement(rawId, tier, rawEnchant, RawQuantityForTier(tier)));

        if (tier > 2)
        {
            var previousTier = tier - 1;
            var previousEnchant = previousTier >= 4 && !recipe.RefinedBase.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase) ? enchant : 0;
            var previousRefined = BuildResourceItemId(previousTier, recipe.RefinedBase, previousEnchant);
            requirements.Add(new RefiningMaterialRequirement(previousRefined, previousTier, previousEnchant, 1));
        }

        return requirements;
    }

    private static int RawQuantityForTier(int tier) => tier switch
    {
        <= 2 => 1,
        3 => 2,
        4 => 2,
        5 => 3,
        6 => 4,
        7 => 5,
        _ => 5
    };

    private static string BuildResourceItemId(int tier, string resourceBase, int enchant)
    {
        tier = Math.Clamp(tier, 2, 8);
        enchant = Math.Clamp(enchant, 0, 4);
        var id = $"T{tier}_{resourceBase}";

        if (enchant <= 0 || tier < 4 || resourceBase.Equals("STONEBLOCK", StringComparison.OrdinalIgnoreCase))
            return id;

        return $"{id}_LEVEL{enchant}@{enchant}";
    }

    private sealed record RefiningResourceInfo(string Key, string RawBase, string RefinedBase, string NamePtBr, string NameEn)
    {
        public static RefiningResourceInfo From(string? resource)
        {
            var key = (resource ?? string.Empty).Trim().ToLowerInvariant();
            return key switch
            {
                "wood" or "logs" or "troncos" or "madeira" => new RefiningResourceInfo("wood", "WOOD", "PLANKS", "Troncos", "Logs"),
                "stone" or "rock" or "pedras" or "pedra" => new RefiningResourceInfo("stone", "ROCK", "STONEBLOCK", "Pedras", "Stone"),
                "ore" or "minerio" or "minério" or "minerio de ferro" or "minério de ferro" => new RefiningResourceInfo("ore", "ORE", "METALBAR", "Minério de ferro", "Ore"),
                "hide" or "pelego" or "couro bruto" => new RefiningResourceInfo("hide", "HIDE", "LEATHER", "Pelego", "Hide"),
                _ => new RefiningResourceInfo("wood", "WOOD", "PLANKS", "Troncos", "Logs")
            };
        }
    }

    private static string Key(MarketPrice price) => $"{price.ItemId}|{price.City}";
}
