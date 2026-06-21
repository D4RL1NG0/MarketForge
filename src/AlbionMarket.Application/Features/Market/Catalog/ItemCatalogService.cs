using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AlbionMarket.Application.Features.Market.Catalog;

public class ItemCatalogService
{
    private static readonly Regex FriendlyNameLineRegex = new(@"^\s*\d+\s*:\s*(?<id>[^:]+?)\s*:\s*(?<name>.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex TierPrefixRegex = new(@"^T(?<tier>\d+)_", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResourceLevelRegex = new(@"_LEVEL(?<level>[1-4])$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> LowValueSearchWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "adept", "adepts", "expert", "experts", "master", "masters", "grandmaster", "grandmasters", "elder", "elders",
        "journeyman", "journeymans", "novice", "novices", "item", "items", "the", "of", "and"
    };

    private readonly List<ItemCatalogItem> _items = new();
    private readonly Dictionary<string, ItemCatalogItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CraftingRecipeOption>> _craftingRecipesByBaseId = new(StringComparer.OrdinalIgnoreCase);

    public void Load()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Catalog", "Source");
        var xmlPath = Path.Combine(sourcePath, "items.xml");
        var txtPath = Path.Combine(sourcePath, "items.txt");
        var ptBrPath = Path.Combine(sourcePath, "items.pt-BR.txt");

        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"Catalog XML not found: {xmlPath}");

        if (!File.Exists(txtPath))
            throw new FileNotFoundException($"Friendly names TXT not found: {txtPath}");

        try
        {
            var xmlItems = LoadXmlItems(xmlPath);
            var friendlyNames = LoadFriendlyNames(txtPath);
            var ptBrNames = File.Exists(ptBrPath) ? LoadFriendlyNames(ptBrPath) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var craftingRecipes = LoadCraftingRecipes(xmlPath);

            _items.Clear();
            _itemsById.Clear();
            _craftingRecipesByBaseId.Clear();
            foreach (var pair in craftingRecipes)
                _craftingRecipesByBaseId[pair.Key] = pair.Value;

            foreach (var xmlItem in xmlItems.Values)
            {
                if (!friendlyNames.TryGetValue(xmlItem.ItemId, out var name))
                    name = PrettifyItemId(xmlItem.ItemId);

                xmlItem.Name = name;
                if (ptBrNames.TryGetValue(xmlItem.ItemId, out var ptName))
                    xmlItem.NamePtBr = ptName;

                _items.Add(xmlItem);
                _itemsById[xmlItem.ItemId] = xmlItem;
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"✅ Loaded {_items.Count} XML catalog items and {_craftingRecipesByBaseId.Count} real crafting/refining recipe entries");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading XML/TXT catalog: {ex.Message}");
            throw;
        }
    }

    public List<ItemCatalogItem> Search(string query, string? tier = null, int? enchant = null, int limit = 60, string? category = null, string? subCategory = null)
    {
        var q = query?.Trim() ?? string.Empty;
        var tierNumber = ParseTier(tier);
        var enchantNumber = NormalizeEnchant(enchant);
        limit = Math.Clamp(limit, 1, 150);

        IEnumerable<ItemCatalogItem> result = _items.Where(IsMarketBrowsableItem);

        if (tierNumber.HasValue)
            result = result.Where(x => x.Tier == tierNumber.Value);

        if (!string.IsNullOrWhiteSpace(category))
            result = result.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(subCategory))
            result = result.Where(x => string.Equals(GetBrowseSubCategoryKey(x.SubCategory), subCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var queryInfo = BuildSearchInfo(q);

            result = result
                .Select(x => new { Item = x, Score = GetScore(x, queryInfo) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Name.Length)
                .ThenBy(x => x.Item.Name)
                .Select(x => x.Item);
        }

        result = CollapseEnchantFamilies(result, enchantNumber);

        return result
            .Select(x => ApplyEnchant(x, enchantNumber))
            .GroupBy(x => MarketFamilyKey(x.ItemId), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(limit)
            .ToList();
    }

    public ItemCatalogItem? ResolveCatalogItem(string itemIdOrName, int? tier = null, int? enchant = null)
    {
        if (string.IsNullOrWhiteSpace(itemIdOrName))
            return null;

        var value = itemIdOrName.Trim();
        var baseId = RemoveEnchant(value);
        var tierNumber = tier is >= 4 and <= 8 ? tier.Value : ExtractTier(baseId);
        var enchantNumber = NormalizeEnchant(enchant);

        ItemCatalogItem? item = null;

        if (_itemsById.TryGetValue(baseId, out var byId))
        {
            item = byId;
        }
        else
        {
            var queryInfo = BuildSearchInfo(value);
            item = _items
                .Where(x => tierNumber <= 0 || x.Tier == tierNumber)
                .Select(x => new { Item = x, Score = GetScore(x, queryInfo) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => string.Equals(x.Item.Name, value, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Score)
                .ThenBy(x => x.Item.Name.Length)
                .Select(x => x.Item)
                .FirstOrDefault();
        }

        if (item is null)
        {
            var fallbackId = tierNumber > 0 ? ReplaceTier(baseId, tierNumber) : baseId;
            fallbackId = ApplyEnchantToId(fallbackId, enchantNumber);
            return new ItemCatalogItem
            {
                ItemId = fallbackId,
                Name = PrettifyItemId(RemoveEnchant(fallbackId)),
                NamePtBr = string.Empty,
                Tier = ExtractTier(fallbackId),
                Enchantment = enchantNumber,
                Category = "other",
                SubCategory = "other"
            };
        }

        var resolvedId = ResolveItemIdForTierAndEnchant(item.ItemId, tierNumber > 0 ? tierNumber : item.Tier, enchantNumber);

        return new ItemCatalogItem
        {
            ItemId = resolvedId,
            Name = GetDisplayNameForResolvedItem(item, resolvedId),
            NamePtBr = GetDisplayNamePtBrForResolvedItem(item, resolvedId),
            Tier = tierNumber > 0 ? tierNumber : item.Tier,
            Enchantment = enchantNumber,
            Category = item.Category,
            SubCategory = item.SubCategory
        };
    }

    public string ResolveMarketItemId(string itemIdOrName, int? tier = null, int? enchant = null)
    {
        return ResolveCatalogItem(itemIdOrName, tier, enchant)?.ItemId ?? string.Empty;
    }

    public string ResolveFriendlyName(string itemIdOrName, int? tier = null, int? enchant = null)
    {
        return ResolveCatalogItem(itemIdOrName, tier, enchant)?.Name ?? itemIdOrName;
    }

    public List<CatalogCategoryOption> GetCategories()
    {
        static string FamilyKey(ItemCatalogItem item)
        {
            var baseId = RemoveEnchant(item.ItemId);
            if (baseId.Length >= 3 && baseId[0] is 'T' or 't' && char.IsDigit(baseId[1]) && baseId[2] == '_')
                return baseId[3..];

            return baseId;
        }

        return _items
            .Where(IsMarketBrowsableItem)
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(categoryGroup => new CatalogCategoryOption
            {
                Key = categoryGroup.Key,
                Name = GetCategoryLabel(categoryGroup.Key),
                Count = categoryGroup.Select(FamilyKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Groups = categoryGroup
                    .GroupBy(x => GetBrowseSubCategoryKey(x.SubCategory), StringComparer.OrdinalIgnoreCase)
                    .Select(subGroup => new CatalogSubCategoryOption
                    {
                        Key = subGroup.Key,
                        Name = GetSubCategoryLabel(subGroup.Key),
                        Count = subGroup.Select(FamilyKey).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                    })
                    .Where(x => x.Count > 0)
                    .OrderBy(x => GetSubCategoryOrder(x.Key))
                    .ThenBy(x => x.Name)
                    .ToList()
            })
            .Where(x => x.Count > 0)
            .OrderBy(x => GetCategoryOrder(x.Key))
            .ThenBy(x => x.Name)
            .ToList();
    }


    public IReadOnlyList<CraftingRecipeOption> GetCraftingRecipeOptions(string itemIdOrName, int? tier = null, int? enchant = null)
    {
        var resolved = ResolveCatalogItem(itemIdOrName, tier, enchant);
        if (resolved is null || string.IsNullOrWhiteSpace(resolved.ItemId))
            return Array.Empty<CraftingRecipeOption>();

        var baseId = RemoveEnchant(resolved.ItemId);
        var selectedEnchant = GetResourceLevel(baseId);
        if (selectedEnchant == 0)
            selectedEnchant = NormalizeEnchant(enchant);

        if (!_craftingRecipesByBaseId.TryGetValue(baseId, out var options))
            return Array.Empty<CraftingRecipeOption>();

        var exact = options
            .Where(x => x.Enchantment == selectedEnchant)
            .Where(x => x.Ingredients.Count > 0)
            .ToList();

        // Alguns itens do XML têm a variação encantada como item separado (ex.: T6_LEATHER_LEVEL1),
        // enquanto equipamentos guardam as receitas dentro de <enchantments>. Se só existir uma receita
        // base segura, use-a como fallback em vez de bloquear a ferramenta.
        if (exact.Count == 0 && selectedEnchant > 0)
        {
            exact = options
                .Where(x => x.Enchantment == 0)
                .Where(x => x.Ingredients.Count > 0)
                .ToList();
        }

        return exact;
    }

    public List<int> GetAvailableTiers()
    {
        return _items
            .Select(x => x.Tier)
            .Where(x => x is >= 4 and <= 8)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public List<int> GetAvailableEnchantments()
    {
        return new List<int> { 0, 1, 2, 3, 4 };
    }

    private static Dictionary<string, ItemCatalogItem> LoadXmlItems(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath, LoadOptions.None);
        var items = new Dictionary<string, ItemCatalogItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.Descendants())
        {
            var id = AttributeValue(element, "uniquename");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            id = RemoveEnchant(id.Trim());
            if (items.ContainsKey(id))
                continue;

            var tier = ExtractTier(id);
            if (tier <= 0)
                tier = ParseInt(AttributeValue(element, "tier")) ?? 0;

            // Evita jogar referências internas/recipe-only no autocomplete quando elas não são itens reais de mercado.
            var elementName = element.Name.LocalName;
            if (string.Equals(elementName, "craftresource", StringComparison.OrdinalIgnoreCase))
                continue;

            items[id] = new ItemCatalogItem
            {
                ItemId = id,
                Name = id,
                NamePtBr = string.Empty,
                Tier = tier,
                Enchantment = ParseInt(AttributeValue(element, "enchantmentlevel")) ?? GetResourceLevel(id),
                Category = AttributeValue(element, "shopcategory") ?? "other",
                SubCategory = AttributeValue(element, "shopsubcategory1") ?? "other"
            };
        }

        return items;
    }


    private static Dictionary<string, List<CraftingRecipeOption>> LoadCraftingRecipes(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath, LoadOptions.None);
        var recipes = new Dictionary<string, List<CraftingRecipeOption>>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.Descendants())
        {
            var rawId = AttributeValue(element, "uniquename");
            if (string.IsNullOrWhiteSpace(rawId))
                continue;

            if (string.Equals(element.Name.LocalName, "craftresource", StringComparison.OrdinalIgnoreCase))
                continue;

            var baseItemId = RemoveEnchant(rawId.Trim());
            if (ExtractTier(baseItemId) <= 0)
                continue;

            var baseEnchant = GetResourceLevel(baseItemId);
            AddCraftingRequirements(recipes, baseItemId, element.Elements("craftingrequirements"), baseEnchant);

            var enchantments = element.Element("enchantments");
            if (enchantments is null)
                continue;

            foreach (var enchantment in enchantments.Elements("enchantment"))
            {
                var enchantLevel = ParseInt(AttributeValue(enchantment, "enchantmentlevel")) ?? 0;
                if (enchantLevel is < 1 or > 4)
                    continue;

                AddCraftingRequirements(recipes, baseItemId, enchantment.Elements("craftingrequirements"), enchantLevel);
            }
        }

        return recipes;
    }

    private static void AddCraftingRequirements(Dictionary<string, List<CraftingRecipeOption>> recipes, string itemId, IEnumerable<XElement> requirementElements, int enchantment)
    {
        foreach (var requirement in requirementElements)
        {
            var resourceElements = requirement.Elements("craftresource").ToList();
            var outputQuantity = Math.Max(1, ParseInt(AttributeValue(requirement, "amountcrafted")) ?? 1);
            var recipeEnchant = ResolveRecipeEnchantment(enchantment, resourceElements);
            var ingredients = resourceElements
                .Select(x => new CraftingRecipeIngredient(
                    NormalizeCraftResourceId(AttributeValue(x, "uniquename") ?? string.Empty, ParseInt(AttributeValue(x, "enchantmentlevel")) ?? 0),
                    Math.Max(1, ParseInt(AttributeValue(x, "count")) ?? 1),
                    !string.Equals(AttributeValue(x, "maxreturnamount"), "0", StringComparison.OrdinalIgnoreCase)))
                .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
                .ToList();

            if (ingredients.Count == 0)
                continue;

            if (!recipes.TryGetValue(itemId, out var options))
            {
                options = new List<CraftingRecipeOption>();
                recipes[itemId] = options;
            }

            // Evita duplicar exatamente a mesma opção de receita quando o dump repete requisitos equivalentes.
            var signature = string.Join('|', ingredients.OrderBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.ItemId}:{x.Quantity}:{x.IsReturnable}"));
            if (options.Any(x => x.Enchantment == recipeEnchant && x.OutputQuantity == outputQuantity && string.Join('|', x.Ingredients.OrderBy(y => y.ItemId, StringComparer.OrdinalIgnoreCase).Select(y => $"{y.ItemId}:{y.Quantity}:{y.IsReturnable}")) == signature))
                continue;

            options.Add(new CraftingRecipeOption(itemId, recipeEnchant, outputQuantity, ingredients));
        }
    }

    private static int ResolveRecipeEnchantment(int parentEnchantment, IReadOnlyCollection<XElement> resourceElements)
    {
        if (parentEnchantment is >= 1 and <= 4)
            return parentEnchantment;

        // Alguns recursos refinados, especialmente blocos de pedra, guardam todas as receitas
        // dentro do item base: T5_STONEBLOCK possui receitas com T5_ROCK, T5_ROCK_LEVEL1,
        // T5_ROCK_LEVEL2 e T5_ROCK_LEVEL3. Se não derivarmos o encantamento da receita,
        // a ferramenta tenta consultar todos os níveis e estoura rate limit na Albion Data.
        var maxResourceEnchant = resourceElements
            .Select(x =>
            {
                var attrEnchant = ParseInt(AttributeValue(x, "enchantmentlevel")) ?? 0;
                var idEnchant = GetResourceLevel(AttributeValue(x, "uniquename") ?? string.Empty);
                return Math.Max(attrEnchant, idEnchant);
            })
            .Where(x => x is >= 1 and <= 4)
            .DefaultIfEmpty(0)
            .Max();

        return maxResourceEnchant;
    }

    private static string NormalizeCraftResourceId(string itemId, int enchantment)
    {
        var clean = itemId.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return string.Empty;

        if (clean.Contains('@'))
            return clean;

        var resolvedEnchant = enchantment is >= 1 and <= 4 ? enchantment : GetResourceLevel(clean);
        if (resolvedEnchant is >= 1 and <= 4)
            return $"{clean}@{resolvedEnchant}";

        return clean;
    }

    private static Dictionary<string, string> LoadFriendlyNames(string txtPath)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(txtPath))
        {
            var match = FriendlyNameLineRegex.Match(line);
            if (!match.Success)
                continue;

            var id = RemoveEnchant(match.Groups["id"].Value.Trim());
            var name = match.Groups["name"].Value.Trim();

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                names[id] = name;
        }

        return names;
    }

    private static string? AttributeValue(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int GetScore(ItemCatalogItem item, SearchInfo query)
    {
        var name = NormalizeForSearch(item.Name);
        var ptName = NormalizeForSearch(item.NamePtBr);
        var id = NormalizeForSearch(item.ItemId.Replace('_', ' '));
        var compactId = NormalizeForSearch(item.ItemId.Replace("_", string.Empty));
        var category = NormalizeForSearch($"{item.Category} {item.SubCategory}");

        var score = 0;


        if (!string.IsNullOrWhiteSpace(ptName))
        {
            if (ptName.Equals(query.Normalized)) score += 1100;
            if (ptName.StartsWith(query.Normalized)) score += 850;
            if (NameWithoutTierPrefix(ptName).Equals(query.Normalized)) score += 780;
            if (NameWithoutTierPrefix(ptName).StartsWith(query.Normalized)) score += 700;
            if (AllTokensPresent(ptName, query.Tokens)) score += 520 + query.Tokens.Length * 30;
            if (AnyTokenStartsWord(ptName, query.Tokens)) score += 280;
            if (ptName.Contains(query.Normalized)) score += 240;
        }

        if (name.Equals(query.Normalized)) score += 1000;
        if (name.StartsWith(query.Normalized)) score += 800;
        if (NameWithoutTierPrefix(name).Equals(query.Normalized)) score += 750;
        if (NameWithoutTierPrefix(name).StartsWith(query.Normalized)) score += 650;

        if (AllTokensPresent(name, query.Tokens)) score += 450 + query.Tokens.Length * 25;
        if (AnyTokenStartsWord(name, query.Tokens)) score += 250;
        if (name.Contains(query.Normalized)) score += 220;

        // ID ajuda para busca técnica, mas não deve dominar quando o usuário digitou nome amigável.
        if (query.LooksLikeItemId)
        {
            if (id.Equals(query.Normalized) || compactId.Equals(query.Compact)) score += 900;
            if (id.StartsWith(query.Normalized) || compactId.StartsWith(query.Compact)) score += 600;
            if (id.Contains(query.Normalized) || compactId.Contains(query.Compact)) score += 300;
        }
        else if (category.Contains(query.Normalized))
        {
            score += 110;
        }

        if (!string.Equals(item.Name, item.ItemId, StringComparison.OrdinalIgnoreCase))
            score += 120;
        else
            score -= 200;

        // Em busca por weapon/armor genérico, itens reais de mercado devem vir antes de artefatos/material interno.
        if (item.ItemId.Contains("ARTEFACT", StringComparison.OrdinalIgnoreCase))
            score -= 80;

        return Math.Max(0, score);
    }

    private ItemCatalogItem ApplyEnchant(ItemCatalogItem item, int enchant)
    {
        var resolvedId = ResolveItemIdForTierAndEnchant(item.ItemId, item.Tier, enchant);

        return new ItemCatalogItem
        {
            ItemId = resolvedId,
            Name = GetDisplayNameForResolvedItem(item, resolvedId),
            NamePtBr = GetDisplayNamePtBrForResolvedItem(item, resolvedId),
            Tier = item.Tier,
            Enchantment = enchant,
            Category = item.Category,
            SubCategory = item.SubCategory
        };
    }

    private IEnumerable<ItemCatalogItem> CollapseEnchantFamilies(IEnumerable<ItemCatalogItem> items, int enchant)
    {
        return items
            .GroupBy(x => MarketFamilyKey(x.ItemId), StringComparer.OrdinalIgnoreCase)
            .Select(group => PickVariantForEnchant(group, enchant));
    }

    private static ItemCatalogItem PickVariantForEnchant(IEnumerable<ItemCatalogItem> group, int enchant)
    {
        var ordered = group.OrderBy(x => x.Name).ToList();
        if (ordered.Count == 0)
            throw new InvalidOperationException("Grupo de item vazio no catálogo.");

        if (enchant <= 0)
            return ordered.FirstOrDefault(x => GetResourceLevel(RemoveEnchant(x.ItemId)) == 0) ?? ordered.First();

        return ordered.FirstOrDefault(x => GetResourceLevel(RemoveEnchant(x.ItemId)) == enchant)
               ?? ordered.FirstOrDefault(x => GetResourceLevel(RemoveEnchant(x.ItemId)) == 0)
               ?? ordered.First();
    }

    private string ResolveItemIdForTierAndEnchant(string itemId, int tier, int enchant)
    {
        var tieredId = tier > 0 ? ReplaceTier(itemId, tier) : RemoveEnchant(itemId);
        var familyKey = MarketFamilyKey(tieredId);

        if (IsResourceLevelFamily(tieredId))
        {
            var desiredBaseId = enchant <= 0 ? familyKey : $"{familyKey}_LEVEL{enchant}";
            desiredBaseId = tier > 0 ? ReplaceTier(desiredBaseId, tier) : desiredBaseId;

            if (_itemsById.ContainsKey(desiredBaseId))
                return ApplyEnchantToId(desiredBaseId, enchant);
        }

        return ApplyEnchantToId(tieredId, enchant);
    }

    private string GetDisplayNameForResolvedItem(ItemCatalogItem selectedItem, string resolvedId)
    {
        var cleanResolvedId = RemoveEnchant(resolvedId);

        if (_itemsById.TryGetValue(cleanResolvedId, out var resolvedItem))
        {
            // Para recursos encantados, o encantamento já aparece no filtro .1/.2/.3/.4.
            // A UI deve mostrar a família do recurso, não duplicar "Incomum/Raro" como item separado.
            if (IsResourceLevelFamily(cleanResolvedId))
            {
                var baseId = MarketFamilyKey(cleanResolvedId);
                if (_itemsById.TryGetValue(baseId, out var baseItem))
                    return baseItem.Name;
            }

            return resolvedItem.Name;
        }

        return selectedItem.Name;
    }

    private string GetDisplayNamePtBrForResolvedItem(ItemCatalogItem selectedItem, string resolvedId)
    {
        var cleanResolvedId = RemoveEnchant(resolvedId);

        if (_itemsById.TryGetValue(cleanResolvedId, out var resolvedItem))
        {
            if (IsResourceLevelFamily(cleanResolvedId))
            {
                var baseId = MarketFamilyKey(cleanResolvedId);
                if (_itemsById.TryGetValue(baseId, out var baseItem) && !string.IsNullOrWhiteSpace(baseItem.NamePtBr))
                    return baseItem.NamePtBr;
            }

            return resolvedItem.NamePtBr;
        }

        return selectedItem.NamePtBr;
    }

    private static bool IsResourceLevelFamily(string itemId)
    {
        var baseId = RemoveEnchant(itemId);
        return ResourceLevelRegex.IsMatch(baseId)
               || baseId.Contains("_ORE", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_WOOD", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_ROCK", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_HIDE", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_FIBER", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_PLANKS", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_STONEBLOCK", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_METALBAR", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_LEATHER", StringComparison.OrdinalIgnoreCase)
               || baseId.Contains("_CLOTH", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetResourceLevel(string itemId)
    {
        var match = ResourceLevelRegex.Match(RemoveEnchant(itemId));
        return match.Success && int.TryParse(match.Groups["level"].Value, out var level) ? level : 0;
    }

    private static string MarketFamilyKey(string itemId)
    {
        var baseId = RemoveEnchant(itemId);
        return ResourceLevelRegex.Replace(baseId, string.Empty, 1);
    }

    private static string ApplyEnchantToId(string itemId, int enchant)
    {
        var baseId = RemoveEnchant(itemId);
        return enchant > 0 ? $"{baseId}@{enchant}" : baseId;
    }

    private static string RemoveEnchant(string itemId)
    {
        var index = itemId.IndexOf('@');
        return index >= 0 ? itemId[..index] : itemId;
    }

    private static string ReplaceTier(string itemId, int tier)
    {
        var baseId = RemoveEnchant(itemId);
        return TierPrefixRegex.Replace(baseId, $"T{tier}_", 1);
    }

    private static int? ParseTier(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier))
            return null;

        var cleaned = tier.Trim().TrimStart('T', 't');
        return int.TryParse(cleaned, out var value) && value is >= 4 and <= 8 ? value : null;
    }

    private static int NormalizeEnchant(int? enchant)
    {
        return enchant is >= 1 and <= 4 ? enchant.Value : 0;
    }

    private static int ExtractTier(string? uniqueName)
    {
        if (string.IsNullOrWhiteSpace(uniqueName))
            return 0;

        var match = TierPrefixRegex.Match(uniqueName);
        return match.Success && int.TryParse(match.Groups["tier"].Value, out var tier) ? tier : 0;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static SearchInfo BuildSearchInfo(string query)
    {
        var normalized = NormalizeForSearch(query);
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2 && !LowValueSearchWords.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0 && !string.IsNullOrWhiteSpace(normalized))
            tokens = new[] { normalized };

        return new SearchInfo(
            normalized,
            normalized.Replace(" ", string.Empty),
            tokens,
            query.Contains('_') || query.Contains('@') || TierPrefixRegex.IsMatch(query.Trim()));
    }

    private static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = RemoveDiacritics(value).ToLowerInvariant();
        value = value.Replace("'s", "s").Replace("’s", "s");
        value = Regex.Replace(value, @"[^a-z0-9]+", " ");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private static string NameWithoutTierPrefix(string normalizedName)
    {
        foreach (var prefix in LowValueSearchWords)
        {
            var p = NormalizeForSearch(prefix);
            if (normalizedName.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase))
                return normalizedName[(p.Length + 1)..];
        }

        return normalizedName;
    }

    private static bool AllTokensPresent(string text, string[] tokens)
    {
        return tokens.Length > 0 && tokens.All(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool AnyTokenStartsWord(string text, string[] tokens)
    {
        if (tokens.Length == 0)
            return false;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token => words.Any(word => word.StartsWith(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsMarketBrowsableItem(ItemCatalogItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ItemId))
            return false;

        if (item.ItemId.StartsWith("QUESTITEM_", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.StartsWith("UNIQUE_LOOTCHEST", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("_NONTRADABLE", StringComparison.OrdinalIgnoreCase)
            || item.ItemId.Contains("_TOKENLOCKED", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static int GetCategoryOrder(string category)
    {
        return NormalizeCategoryKey(category) switch
        {
            "weapons" => 10,
            "head" => 20,
            "armors" => 30,
            "shoes" => 40,
            "offhands" => 50,
            "bags" => 60,
            "capes" => 70,
            "mounts" => 80,
            "consumables" => 90,
            "gathering" => 100,
            "crafting" => 110,
            "farming" => 120,
            "furniture" => 130,
            "artefacts" => 140,
            "vanity" => 150,
            "other" => 999,
            _ => 500
        };
    }

    private static string GetBrowseSubCategoryKey(string subCategory)
    {
        var key = NormalizeCategoryKey(subCategory);
        return key switch
        {
            "ore_rare" => "ore",
            "rock_rare" => "rock",
            "wood_rare" => "wood",
            "fiber_rare" => "fiber",
            "hide_rare" => "hide",
            "resources_rare" => "resources",
            "refinedresources_rare" => "refinedresources",
            _ => key
        };
    }

    private static int GetSubCategoryOrder(string subCategory)
    {
        return NormalizeCategoryKey(subCategory) switch
        {
            "sword" => 10,
            "axe" => 20,
            "mace" => 30,
            "hammer" => 40,
            "spear" => 50,
            "dagger" => 60,
            "bow" => 70,
            "crossbow" => 80,
            "firestaff" => 90,
            "froststaff" => 100,
            "arcanestaff" => 110,
            "cursestaff" => 120,
            "holystaff" => 130,
            "naturestaff" => 140,
            "quarterstaff" => 150,
            "knuckles" => 160,
            "shapeshifterstaff" => 170,
            "bags" => 180,
            "accessoires_capes_capes" => 190,
            "basemounts" => 200,
            "raremounts" => 210,
            "food" => 220,
            "potions" => 230,
            "resources" => 240,
            "refinedresources" => 250,
            "other" => 999,
            _ => 500
        };
    }

    private static string GetCategoryLabel(string category)
    {
        return NormalizeCategoryKey(category) switch
        {
            "weapons" => "Armas",
            "head" => "Cabeças",
            "armors" => "Armaduras",
            "shoes" => "Botas",
            "offhands" => "Mãos secundárias",
            "bags" => "Bolsas",
            "capes" => "Capas",
            "mounts" => "Montarias",
            "consumables" => "Consumíveis",
            "gathering" => "Coleta",
            "crafting" => "Recursos / Craft",
            "farming" => "Fazenda",
            "furniture" => "Mobília",
            "artefacts" => "Artefatos",
            "vanity" => "Aparência / Vanity",
            "other" => "Outros",
            _ => ToTitleLabel(category)
        };
    }

    private static string GetSubCategoryLabel(string subCategory)
    {
        return NormalizeCategoryKey(subCategory) switch
        {
            "sword" => "Espadas",
            "axe" => "Machados",
            "mace" => "Maças",
            "hammer" => "Martelos",
            "spear" => "Lanças",
            "dagger" => "Adagas",
            "bow" => "Arcos",
            "crossbow" => "Bestas",
            "firestaff" => "Cajados de fogo",
            "froststaff" => "Cajados de gelo",
            "arcanestaff" => "Cajados arcanos",
            "cursestaff" => "Cajados amaldiçoados",
            "holystaff" => "Cajados sagrados",
            "naturestaff" => "Cajados da natureza",
            "quarterstaff" => "Bastões",
            "knuckles" => "Luvas de guerra",
            "shapeshifterstaff" => "Cajados metamorfos",
            "plate_helmet" => "Capacetes de placa",
            "leather_helmet" => "Capuzes de couro",
            "cloth_helmet" => "Capuzes de tecido",
            "plate_armor" => "Armaduras de placa",
            "leather_armor" => "Jaquetas de couro",
            "cloth_armor" => "Túnicas de tecido",
            "plate_shoes" => "Botas de placa",
            "leather_shoes" => "Sapatos de couro",
            "cloth_shoes" => "Sandálias de tecido",
            "shieldtype" => "Escudos",
            "booktype" => "Livros",
            "torchtype" => "Tochas",
            "bags" => "Bolsas",
            "accessoires_capes_capes" => "Capas comuns",
            "accessoires_capes_bridgewatch" => "Capas Bridgewatch",
            "accessoires_capes_fortsterling" => "Capas Fort Sterling",
            "accessoires_capes_lymhurst" => "Capas Lymhurst",
            "accessoires_capes_martlock" => "Capas Martlock",
            "accessoires_capes_thetford" => "Capas Thetford",
            "accessoires_capes_caerleon" => "Capas Caerleon",
            "basemounts" => "Montarias comuns",
            "raremounts" => "Montarias raras",
            "battle_mount" => "Montarias de batalha",
            "food" => "Comidas",
            "potions" => "Poções",
            "tomes" => "Tomes / Livros",
            "resources" => "Recursos brutos",
            "refinedresources" => "Recursos refinados",
            "fish" => "Peixes",
            "alchemy" => "Alquimia",
            "ore" => "Minério",
            "rock" => "Pedra",
            "wood" => "Madeira",
            "fiber" => "Fibra",
            "hide" => "Couro",
            "farm" => "Plantações",
            "herbgarden" => "Ervas",
            "pasture" => "Pasto",
            "kennel" => "Canil",
            "farmingproducts" => "Produtos de fazenda",
            "world" => "Mundo",
            "island" => "Ilha",
            "house" => "Casa",
            "chest" => "Baús",
            "weapons" => "Artefatos de armas",
            "armors" => "Artefatos de armadura",
            "head" => "Artefatos de cabeça",
            "shoes" => "Artefatos de botas",
            "offhands" => "Artefatos de mão secundária",
            "capes" => "Artefatos de capa",
            "fragments" => "Runas / Almas / Relíquias",
            "other" => "Outros",
            _ => ToTitleLabel(subCategory)
        };
    }

    private static string NormalizeCategoryKey(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string ToTitleLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Outros";

        var cleaned = value.Replace('_', ' ').Replace("accessoires", "accessories", StringComparison.OrdinalIgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned);
    }

    private static string PrettifyItemId(string itemId)
    {
        var baseId = RemoveEnchant(itemId);
        baseId = TierPrefixRegex.Replace(baseId, string.Empty, 1);
        baseId = baseId.Replace("2H", "Two-handed", StringComparison.OrdinalIgnoreCase)
            .Replace("MAIN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("OFF", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ');

        baseId = Regex.Replace(baseId, @"\s+", " ").Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(baseId);
    }

    private sealed record SearchInfo(string Normalized, string Compact, string[] Tokens, bool LooksLikeItemId);

}

public sealed record CraftingRecipeOption(string ItemId, int Enchantment, int OutputQuantity, IReadOnlyList<CraftingRecipeIngredient> Ingredients);
public sealed record CraftingRecipeIngredient(string ItemId, int Quantity, bool IsReturnable);

