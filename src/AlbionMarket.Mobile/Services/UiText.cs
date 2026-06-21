using System.Globalization;
using System.Text.RegularExpressions;
using AlbionMarket.Mobile.Models;

namespace AlbionMarket.Mobile.Services;

public enum AppLanguage
{
    PtBr,
    En
}

public static class UiText
{
    private static readonly Dictionary<string, string> CategoryPt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapons"] = "Armas",
        ["head"] = "Cabeças",
        ["armors"] = "Armaduras",
        ["shoes"] = "Botas",
        ["offhands"] = "Mãos secundárias",
        ["bags"] = "Bolsas",
        ["capes"] = "Capas",
        ["mounts"] = "Montarias",
        ["consumables"] = "Consumíveis",
        ["gathering"] = "Coleta",
        ["crafting"] = "Recursos e craft",
        ["farming"] = "Fazenda",
        ["furniture"] = "Mobília",
        ["artefacts"] = "Artefatos",
        ["vanity"] = "Aparência",
        ["other"] = "Outros"
    };

    private static readonly Dictionary<string, string> CategoryEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapons"] = "Weapons",
        ["head"] = "Head",
        ["armors"] = "Armors",
        ["shoes"] = "Shoes",
        ["offhands"] = "Off-hands",
        ["bags"] = "Bags",
        ["capes"] = "Capes",
        ["mounts"] = "Mounts",
        ["consumables"] = "Consumables",
        ["gathering"] = "Gathering",
        ["crafting"] = "Resources and craft",
        ["farming"] = "Farming",
        ["furniture"] = "Furniture",
        ["artefacts"] = "Artifacts",
        ["vanity"] = "Vanity",
        ["other"] = "Other"
    };

    private static readonly Dictionary<string, string> GroupPt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sword"] = "Espadas",
        ["axe"] = "Machados",
        ["mace"] = "Maças",
        ["hammer"] = "Martelos",
        ["spear"] = "Lanças",
        ["dagger"] = "Adagas",
        ["bow"] = "Arcos",
        ["crossbow"] = "Bestas",
        ["firestaff"] = "Cajados de fogo",
        ["froststaff"] = "Cajados de gelo",
        ["arcanestaff"] = "Cajados arcanos",
        ["cursestaff"] = "Cajados amaldiçoados",
        ["holystaff"] = "Cajados sagrados",
        ["naturestaff"] = "Cajados da natureza",
        ["quarterstaff"] = "Bastões",
        ["knuckles"] = "Luvas de guerra",
        ["shapeshifterstaff"] = "Cajados metamorfos",
        ["plate_helmet"] = "Capacetes de placa",
        ["leather_helmet"] = "Capuzes de couro",
        ["cloth_helmet"] = "Capuzes de tecido",
        ["plate_armor"] = "Armaduras de placa",
        ["leather_armor"] = "Jaquetas de couro",
        ["cloth_armor"] = "Túnicas de tecido",
        ["plate_shoes"] = "Botas de placa",
        ["leather_shoes"] = "Botas de couro",
        ["cloth_shoes"] = "Sandálias de tecido",
        ["shieldtype"] = "Escudos",
        ["booktype"] = "Livros",
        ["torchtype"] = "Tochas",
        ["hornstype"] = "Chifres",
        ["orbtype"] = "Orbes",
        ["totemtype"] = "Totens",
        ["bags"] = "Bolsas",
        ["accessoires_capes_capes"] = "Capas comuns",
        ["accessoires_capes_bridgewatch"] = "Capas de Bridgewatch",
        ["accessoires_capes_fortsterling"] = "Capas de Fort Sterling",
        ["accessoires_capes_lymhurst"] = "Capas de Lymhurst",
        ["accessoires_capes_martlock"] = "Capas de Martlock",
        ["accessoires_capes_thetford"] = "Capas de Thetford",
        ["accessoires_capes_caerleon"] = "Capas de Caerleon",
        ["basemounts"] = "Montarias comuns",
        ["raremounts"] = "Montarias raras",
        ["battle_mount"] = "Montarias de batalha",
        ["food"] = "Comidas",
        ["potions"] = "Poções",
        ["tomes"] = "Tomes e livros",
        ["resources"] = "Recursos brutos",
        ["refinedresources"] = "Recursos refinados",
        ["cityresources"] = "Recursos de cidade",
        ["fish"] = "Peixes",
        ["alchemy"] = "Alquimia",
        ["tokens"] = "Tokens",
        ["ore"] = "Minério",
        ["ore_rare"] = "Minério",
        ["rock"] = "Pedra",
        ["rock_rare"] = "Pedra",
        ["wood"] = "Madeira",
        ["wood_rare"] = "Madeira",
        ["fiber"] = "Fibra",
        ["fiber_rare"] = "Fibra",
        ["hide"] = "Couro cru",
        ["hide_rare"] = "Couro cru",
        ["tracking"] = "Rastreamento",
        ["farm"] = "Plantações",
        ["herbgarden"] = "Ervas",
        ["pasture"] = "Pasto",
        ["kennel"] = "Canil",
        ["farmingproducts"] = "Produtos de fazenda",
        ["world"] = "Mundo",
        ["island"] = "Ilha",
        ["house"] = "Casa",
        ["chest"] = "Baús",
        ["weapons"] = "Artefatos de armas",
        ["armors"] = "Artefatos de armadura",
        ["head"] = "Artefatos de cabeça",
        ["shoes"] = "Artefatos de botas",
        ["offhands"] = "Artefatos de mão secundária",
        ["capes"] = "Artefatos de capa",
        ["fragments"] = "Runas, almas e relíquias",
        ["other"] = "Outros"
    };

    private static readonly Dictionary<string, string> ItemPhrasesPt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Adept's"] = "Adepto",
        ["Expert's"] = "Especialista",
        ["Master's"] = "Mestre",
        ["Grandmaster's"] = "Grão-mestre",
        ["Elder's"] = "Ancião",
        ["Journeyman's"] = "Aprendiz",
        ["Novice's"] = "Novato",
        ["Beginner's"] = "Iniciante",
        ["Uncommon"] = "Incomum",
        ["Rare"] = "Raro",
        ["Exceptional"] = "Excepcional",
        ["Pristine"] = "Prístino",

        // Cabeças
        ["Demon Helmet"] = "Capacete demoníaco",
        ["Soldier Helmet"] = "Capacete de soldado",
        ["Knight Helmet"] = "Capacete de cavaleiro",
        ["Guardian Helmet"] = "Capacete de guardião",
        ["Graveguard Helmet"] = "Capacete de guarda tumular",
        ["Judicator Helmet"] = "Capacete de juiz",
        ["Helmet of Valor"] = "Capacete de valor",
        ["Mercenary Hood"] = "Capuz de mercenário",
        ["Hunter Hood"] = "Capuz de caçador",
        ["Assassin Hood"] = "Capuz de assassino",
        ["Stalker Hood"] = "Capuz de perseguidor",
        ["Hellion Hood"] = "Capuz infernal",
        ["Specter Hood"] = "Capuz espectral",
        ["Royal Hood"] = "Capuz real",
        ["Scholar Cowl"] = "Capuz de estudioso",
        ["Cleric Cowl"] = "Capuz de clérigo",
        ["Mage Cowl"] = "Capuz de mago",
        ["Druid Cowl"] = "Capuz de druida",
        ["Cultist Cowl"] = "Capuz de cultista",
        ["Fiend Cowl"] = "Capuz demoníaco",

        // Peitoral
        ["Soldier Armor"] = "Armadura de soldado",
        ["Knight Armor"] = "Armadura de cavaleiro",
        ["Guardian Armor"] = "Armadura de guardião",
        ["Graveguard Armor"] = "Armadura de guarda tumular",
        ["Judicator Armor"] = "Armadura de juiz",
        ["Armor of Valor"] = "Armadura de valor",
        ["Mercenary Jacket"] = "Jaqueta de mercenário",
        ["Hunter Jacket"] = "Jaqueta de caçador",
        ["Assassin Jacket"] = "Jaqueta de assassino",
        ["Stalker Jacket"] = "Jaqueta de perseguidor",
        ["Hellion Jacket"] = "Jaqueta infernal",
        ["Specter Jacket"] = "Jaqueta espectral",
        ["Royal Jacket"] = "Jaqueta real",
        ["Scholar Robe"] = "Túnica de estudioso",
        ["Cleric Robe"] = "Túnica de clérigo",
        ["Mage Robe"] = "Túnica de mago",
        ["Druid Robe"] = "Túnica de druida",
        ["Cultist Robe"] = "Túnica de cultista",
        ["Fiend Robe"] = "Túnica demoníaca",
        ["Royal Robe"] = "Túnica real",

        // Botas
        ["Soldier Boots"] = "Botas de soldado",
        ["Knight Boots"] = "Botas de cavaleiro",
        ["Guardian Boots"] = "Botas de guardião",
        ["Graveguard Boots"] = "Botas de guarda tumular",
        ["Judicator Boots"] = "Botas de juiz",
        ["Boots of Valor"] = "Botas de valor",
        ["Mercenary Shoes"] = "Sapatos de mercenário",
        ["Hunter Shoes"] = "Sapatos de caçador",
        ["Assassin Shoes"] = "Sapatos de assassino",
        ["Stalker Shoes"] = "Sapatos de perseguidor",
        ["Hellion Shoes"] = "Sapatos infernais",
        ["Specter Shoes"] = "Sapatos espectrais",
        ["Royal Shoes"] = "Sapatos reais",
        ["Scholar Sandals"] = "Sandálias de estudioso",
        ["Cleric Sandals"] = "Sandálias de clérigo",
        ["Mage Sandals"] = "Sandálias de mago",
        ["Druid Sandals"] = "Sandálias de druida",
        ["Cultist Sandals"] = "Sandálias de cultista",
        ["Fiend Sandals"] = "Sandálias demoníacas",
        ["Royal Sandals"] = "Sandálias reais",

        // Armas
        ["Broadsword"] = "Espada larga",
        ["Claymore"] = "Claymore",
        ["Dual Swords"] = "Espadas duplas",
        ["Clarent Blade"] = "Lâmina Clarent",
        ["Carving Sword"] = "Espada entalhadora",
        ["Galatine Pair"] = "Par de Galatinas",
        ["Kingmaker"] = "Fazedor de reis",
        ["Battleaxe"] = "Machado de batalha",
        ["Great Axe"] = "Machado grande",
        ["Greataxe"] = "Machado grande",
        ["Halberd"] = "Alabarda",
        ["Carrioncaller"] = "Chamador de carniça",
        ["Infernal Scythe"] = "Foice infernal",
        ["Bear Paws"] = "Patas de urso",
        ["Realmbreaker"] = "Quebra-reino",
        ["Mace"] = "Maça",
        ["Heavy Mace"] = "Maça pesada",
        ["Morning Star"] = "Estrela da manhã",
        ["Bedrock Mace"] = "Maça de rocha",
        ["Incubus Mace"] = "Maça de íncubo",
        ["Camlann Mace"] = "Maça Camlann",
        ["Oathkeepers"] = "Guardiões do juramento",
        ["Hammer"] = "Martelo",
        ["Polehammer"] = "Martelo de haste",
        ["Great Hammer"] = "Martelo grande",
        ["Tombhammer"] = "Martelo tumular",
        ["Forge Hammers"] = "Martelos de forja",
        ["Grovekeeper"] = "Guardião do bosque",
        ["Hand of Justice"] = "Mão da justiça",
        ["Spear"] = "Lança",
        ["Pike"] = "Pique",
        ["Glaive"] = "Glaive",
        ["Heron Spear"] = "Lança de garça",
        ["Spirithunter"] = "Caçador de espíritos",
        ["Trinity Spear"] = "Lança da trindade",
        ["Daybreaker"] = "Rompe-alvorada",
        ["Dagger Pair"] = "Par de adagas",
        ["Dagger"] = "Adaga",
        ["Claws"] = "Garras",
        ["Bloodletter"] = "Sangradora",
        ["Deathgivers"] = "Dadoras da morte",
        ["Bridled Fury"] = "Fúria controlada",
        ["Demonfang"] = "Presa demoníaca",
        ["Bow of Badon"] = "Arco de Badon",
        ["Whispering Bow"] = "Arco sussurrante",
        ["Wailing Bow"] = "Arco lamentador",
        ["Mistpiercer"] = "Perfurador de névoa",
        ["Longbow"] = "Arco longo",
        ["Warbow"] = "Arco de guerra",
        ["Bow"] = "Arco",
        ["Light Crossbow"] = "Besta leve",
        ["Heavy Crossbow"] = "Besta pesada",
        ["Crossbow"] = "Besta",
        ["Weeping Repeater"] = "Repetidora chorosa",
        ["Boltcasters"] = "Lançadores de virotes",
        ["Siegebow"] = "Besta de cerco",
        ["Energy Shaper"] = "Moldador de energia",
        ["Fire Staff"] = "Cajado de fogo",
        ["Great Fire Staff"] = "Cajado de fogo grande",
        ["Infernal Staff"] = "Cajado infernal",
        ["Wildfire Staff"] = "Cajado de fogo selvagem",
        ["Brimstone Staff"] = "Cajado de enxofre",
        ["Blazing Staff"] = "Cajado flamejante",
        ["Dawnsong"] = "Canção da aurora",
        ["Frost Staff"] = "Cajado de gelo",
        ["Great Frost Staff"] = "Cajado de gelo grande",
        ["Glacial Staff"] = "Cajado glacial",
        ["Hoarfrost Staff"] = "Cajado de geada",
        ["Icicle Staff"] = "Cajado de icicle",
        ["Permafrost Prism"] = "Prisma de permafrost",
        ["Chillhowl"] = "Uivo gelado",
        ["Arcane Staff"] = "Cajado arcano",
        ["Great Arcane Staff"] = "Cajado arcano grande",
        ["Enigmatic Staff"] = "Cajado enigmático",
        ["Witchwork Staff"] = "Cajado bruxo",
        ["Occult Staff"] = "Cajado ocultista",
        ["Malevolent Locus"] = "Locus malévolo",
        ["Evensong"] = "Canção do entardecer",
        ["Cursed Staff"] = "Cajado amaldiçoado",
        ["Great Cursed Staff"] = "Cajado amaldiçoado grande",
        ["Demonic Staff"] = "Cajado demoníaco",
        ["Lifecurse Staff"] = "Cajado maldição vital",
        ["Cursed Skull"] = "Crânio amaldiçoado",
        ["Damnation Staff"] = "Cajado da danação",
        ["Shadowcaller"] = "Chamador das sombras",
        ["Holy Staff"] = "Cajado sagrado",
        ["Great Holy Staff"] = "Cajado sagrado grande",
        ["Divine Staff"] = "Cajado divino",
        ["Lifetouch Staff"] = "Cajado toque vital",
        ["Fallen Staff"] = "Cajado caído",
        ["Redemption Staff"] = "Cajado da redenção",
        ["Hallowfall"] = "Queda sagrada",
        ["Nature Staff"] = "Cajado da natureza",
        ["Great Nature Staff"] = "Cajado da natureza grande",
        ["Wild Staff"] = "Cajado selvagem",
        ["Druidic Staff"] = "Cajado druídico",
        ["Blight Staff"] = "Cajado da praga",
        ["Rampant Staff"] = "Cajado desenfreado",
        ["Ironroot Staff"] = "Cajado raiz de ferro",
        ["Quarterstaff"] = "Bastão",
        ["Iron-clad Staff"] = "Bastão revestido de ferro",
        ["Double Bladed Staff"] = "Bastão de lâmina dupla",
        ["Black Monk Stave"] = "Bastão de monge negro",
        ["Soulscythe"] = "Foice de almas",
        ["Staff of Balance"] = "Bastão do equilíbrio",
        ["Grailseeker"] = "Buscador do Graal",
        ["Brawler Gloves"] = "Luvas de lutador",
        ["Battle Bracers"] = "Braceletes de batalha",
        ["Spiked Gauntlets"] = "Manoplas espinhosas",
        ["Ursine Maulers"] = "Dilaceradores ursinos",
        ["Hellfire Hands"] = "Mãos de fogo infernal",
        ["Ravenstrike Cestus"] = "Cestus golpe de corvo",
        ["Fists of Avalon"] = "Punhos de Avalon",

        // Mão secundária / acessórios
        ["Shield"] = "Escudo",
        ["Sarcophagus"] = "Sarcófago",
        ["Caitiff Shield"] = "Escudo Caitiff",
        ["Facebreaker"] = "Quebra-face",
        ["Astral Aegis"] = "Égide astral",
        ["Tome of Spells"] = "Tomo de feitiços",
        ["Eye of Secrets"] = "Olho dos segredos",
        ["Muisak"] = "Muisak",
        ["Taproot"] = "Raiz principal",
        ["Torch"] = "Tocha",
        ["Mistcaller"] = "Chamador da névoa",
        ["Leering Cane"] = "Bengala sinistra",
        ["Cryptcandle"] = "Vela da cripta",
        ["Cape"] = "Capa",
        ["Bag"] = "Bolsa",

        // Montarias e fazenda
        ["Ox Calf"] = "Filhote de boi",
        ["Ox"] = "Boi",
        ["Foal"] = "Potro",
        ["Horse"] = "Cavalo",
        ["Mule"] = "Mula",
        ["Riding Horse"] = "Cavalo de montaria",
        ["Armored Horse"] = "Cavalo blindado",
        ["Transport Ox"] = "Boi de transporte",
        ["Swiftclaw"] = "Garrápida",
        ["Direwolf"] = "Lobo gigante",
        ["Stag"] = "Cervo",
        ["Moose"] = "Alce",
        ["Mammoth"] = "Mamute",
        ["Baby"] = "Filhote",
        ["Seeds"] = "Sementes",
        ["Seed"] = "Semente",
        ["Carrot"] = "Cenoura",
        ["Bean"] = "Feijão",
        ["Wheat"] = "Trigo",
        ["Turnip"] = "Nabo",
        ["Cabbage"] = "Repolho",
        ["Potato"] = "Batata",
        ["Corn"] = "Milho",
        ["Pumpkin"] = "Abóbora",

        // Recursos e consumíveis
        ["Iron Ore"] = "Minério de ferro",
        ["Titanium Ore"] = "Minério de titânio",
        ["Runite Ore"] = "Minério de runita",
        ["Meteorite Ore"] = "Minério de meteorito",
        ["Steel Bar"] = "Barra de aço",
        ["Titanium Steel Bar"] = "Barra de aço titânio",
        ["Runite Steel Bar"] = "Barra de aço runita",
        ["Meteorite Steel Bar"] = "Barra de aço meteorito",
        ["Pine Logs"] = "Troncos de pinheiro",
        ["Cedar Logs"] = "Troncos de cedro",
        ["Bloodoak Logs"] = "Troncos de carvalho-sangue",
        ["Ashenbark Logs"] = "Troncos de casca cinzenta",
        ["Pine Planks"] = "Tábuas de pinheiro",
        ["Cedar Planks"] = "Tábuas de cedro",
        ["Bloodoak Planks"] = "Tábuas de carvalho-sangue",
        ["Ashenbark Planks"] = "Tábuas de casca cinzenta",
        ["Travertine Block"] = "Bloco de travertino",
        ["Granite Block"] = "Bloco de granito",
        ["Slate Block"] = "Bloco de ardósia",
        ["Basalt Block"] = "Bloco de basalto",
        ["Travertine"] = "Travertino",
        ["Granite"] = "Granito",
        ["Slate"] = "Ardósia",
        ["Basalt"] = "Basalto",
        ["Medium Hide"] = "Couro cru médio",
        ["Heavy Hide"] = "Couro cru pesado",
        ["Robust Hide"] = "Couro cru robusto",
        ["Thick Hide"] = "Couro cru espesso",
        ["Worked Leather"] = "Couro trabalhado",
        ["Cured Leather"] = "Couro curtido",
        ["Hardened Leather"] = "Couro endurecido",
        ["Reinforced Leather"] = "Couro reforçado",
        ["Hemp"] = "Cânhamo",
        ["Skyflower"] = "Flor-do-céu",
        ["Redleaf Cotton"] = "Algodão folha-vermelha",
        ["Sunflax"] = "Linho solar",
        ["Fine Cloth"] = "Tecido fino",
        ["Ornate Cloth"] = "Tecido ornamentado",
        ["Lavish Cloth"] = "Tecido luxuoso",
        ["Opulent Cloth"] = "Tecido opulento",
        ["Ore"] = "Minério",
        ["Metal Bar"] = "Barra de metal",
        ["Logs"] = "Troncos",
        ["Planks"] = "Tábuas",
        ["Stone Block"] = "Bloco de pedra",
        ["Hide"] = "Couro cru",
        ["Leather"] = "Couro",
        ["Fiber"] = "Fibra",
        ["Cloth"] = "Tecido",
        ["Potion"] = "Poção",
        ["Food"] = "Comida",
        ["Fish"] = "Peixe"
    };

    private static readonly string[] TierWordsPt =
    {
        "Adepto", "Especialista", "Mestre", "Grão-mestre", "Ancião", "Aprendiz", "Novato", "Iniciante"
    };

    public static string CategoryName(CatalogCategory category, AppLanguage language)
    {
        var name = language == AppLanguage.PtBr
            ? CategoryPt.GetValueOrDefault(category.Key) ?? ToTitlePt(category.Name)
            : CategoryEn.GetValueOrDefault(category.Key) ?? ToTitleEn(category.Name);

        return category.Count > 0 ? $"{name} ({category.Count})" : name;
    }

    public static string GroupName(CatalogGroup group, AppLanguage language)
    {
        var name = language == AppLanguage.PtBr
            ? GroupPt.GetValueOrDefault(group.Key) ?? ToTitlePt(group.Name)
            : ToTitleEn(group.Name);

        return group.Count > 0 ? $"{name} ({group.Count})" : name;
    }

    public static string ItemName(ItemSuggestion item, AppLanguage language)
    {
        var rawName = string.IsNullOrWhiteSpace(item.Name) ? item.ItemId : item.Name;
        if (language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(item.NamePtBr))
            return item.NamePtBr;
        return language == AppLanguage.PtBr ? TranslateItemName(rawName, item.ItemId) : rawName;
    }

    public static string ItemName(MarketOpportunity item, AppLanguage language)
    {
        if (language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(item.ItemNamePtBr))
            return item.ItemNamePtBr;
        var rawName = string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemId : item.ItemName;
        return language == AppLanguage.PtBr ? TranslateItemName(rawName, item.ItemId) : rawName;
    }

    public static string QualityName(int quality, AppLanguage language)
    {
        return language == AppLanguage.En
            ? quality switch
            {
                1 => "Normal",
                2 => "Good",
                3 => "Outstanding",
                4 => "Excellent",
                5 => "Masterpiece",
                _ => "Normal"
            }
            : quality switch
            {
                1 => "Normal",
                2 => "Boa",
                3 => "Notável",
                4 => "Excelente",
                5 => "Obra-prima",
                _ => "Normal"
            };
    }


    public static string SearchQueryForApi(string query, AppLanguage language)
    {
        if (language == AppLanguage.En || string.IsNullOrWhiteSpace(query))
            return query;

        var value = query.Trim().ToLowerInvariant();
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bolsa"] = "bag", ["bolsas"] = "bag", ["capa"] = "cape", ["capas"] = "cape",
            ["espada"] = "sword", ["espadas"] = "sword", ["machado"] = "axe", ["machados"] = "axe",
            ["maça"] = "mace", ["maca"] = "mace", ["martelo"] = "hammer", ["lança"] = "spear", ["lanca"] = "spear",
            ["adaga"] = "dagger", ["arco"] = "bow", ["besta"] = "crossbow", ["cajado"] = "staff",
            ["fogo"] = "fire", ["gelo"] = "frost", ["sagrado"] = "holy", ["natureza"] = "nature", ["amaldiçoado"] = "cursed", ["amaldicoado"] = "cursed",
            ["capacete"] = "helmet", ["capuz"] = "hood", ["armadura"] = "armor", ["jaqueta"] = "jacket", ["túnica"] = "robe", ["tunica"] = "robe",
            ["bota"] = "boots", ["botas"] = "boots", ["sapato"] = "shoes", ["sandalia"] = "sandals", ["sandália"] = "sandals",
            ["escudo"] = "shield", ["tocha"] = "torch", ["livro"] = "book", ["orbe"] = "orb",
            ["cavalo"] = "horse", ["boi"] = "ox", ["montaria"] = "mount", ["lobo"] = "direwolf",
            ["minério"] = "ore", ["minerio"] = "ore", ["madeira"] = "wood", ["pedra"] = "rock", ["fibra"] = "fiber", ["couro"] = "hide",
            ["poção"] = "potion", ["pocao"] = "potion", ["comida"] = "food", ["peixe"] = "fish"
        };

        foreach (var alias in aliases.OrderByDescending(x => x.Key.Length))
            value = Regex.Replace(value, $"\\b{Regex.Escape(alias.Key)}\\b", alias.Value, RegexOptions.IgnoreCase);

        return value;
    }

    public static string ItemIconUrl(string itemId, int quality = 1, int size = 96)
    {
        var cleanId = string.IsNullOrWhiteSpace(itemId) ? "T4_BAG" : itemId.Trim().ToUpperInvariant();
        var encodedId = Uri.EscapeDataString(cleanId).Replace("%40", "@", StringComparison.OrdinalIgnoreCase);
        quality = Math.Clamp(quality, 1, 5);
        size = Math.Clamp(size, 48, 217);
        return $"https://render.albiononline.com/v1/item/{encodedId}.png?quality={quality}&size={size}&locale=en";
    }

    public static string TranslateItemNamePublic(string rawName, string itemId, AppLanguage language)
    {
        return language == AppLanguage.PtBr ? TranslateItemName(rawName, itemId) : rawName;
    }

    private static string TranslateItemName(string rawName, string itemId)
    {
        rawName = StripResourceRarity(rawName, itemId).Trim();
        if (string.IsNullOrWhiteSpace(rawName))
            return itemId;

        // Primeiro trate o padrão oficial "Adept's Demon Helmet" como frase inteira.
        // Isso evita nomes mistos tipo "Demon Helmet do adepto".
        var tierPrefix = ExtractTierPrefix(rawName, out var withoutTier);
        var translatedItem = TranslateItemCore(withoutTier);
        if (!string.IsNullOrWhiteSpace(tierPrefix))
            return $"{CapitalizeFirst(translatedItem)} do {tierPrefix.ToLowerInvariant()}";

        // Depois trate casos em que o texto já veio parcialmente alterado.
        foreach (var tier in TierWordsPt)
        {
            if (rawName.StartsWith(tier + " ", StringComparison.OrdinalIgnoreCase))
            {
                var item = rawName[(tier.Length + 1)..].Trim();
                return $"{CapitalizeFirst(TranslateItemCore(item))} do {tier.ToLowerInvariant()}";
            }

            if (rawName.EndsWith(" do " + tier.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                var item = rawName[..^($" do {tier.ToLowerInvariant()}".Length)].Trim();
                return $"{CapitalizeFirst(TranslateItemCore(item))} do {tier.ToLowerInvariant()}";
            }
        }

        return CapitalizeFirst(TranslateItemCore(rawName));
    }

    private static string ExtractTierPrefix(string rawName, out string withoutTier)
    {
        var tierMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Beginner's"] = "Iniciante",
            ["Novice's"] = "Novato",
            ["Journeyman's"] = "Aprendiz",
            ["Adept's"] = "Adepto",
            ["Expert's"] = "Especialista",
            ["Master's"] = "Mestre",
            ["Grandmaster's"] = "Grão-mestre",
            ["Elder's"] = "Ancião"
        };

        foreach (var tier in tierMap.OrderByDescending(x => x.Key.Length))
        {
            if (rawName.StartsWith(tier.Key + " ", StringComparison.OrdinalIgnoreCase))
            {
                withoutTier = rawName[(tier.Key.Length + 1)..].Trim();
                return tier.Value;
            }
        }

        withoutTier = rawName;
        return string.Empty;
    }

    private static string TranslateItemCore(string value)
    {
        value = value.Trim();

        foreach (var entry in ItemPhrasesPt.OrderByDescending(x => x.Key.Length))
        {
            var pattern = entry.Key.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '-')
                ? Regex.Escape(entry.Key)
                : $"\\b{Regex.Escape(entry.Key)}\\b";
            value = Regex.Replace(value, pattern, entry.Value, RegexOptions.IgnoreCase);
        }

        var wordMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Demon"] = "Demoníaco", ["Soldier"] = "Soldado", ["Knight"] = "Cavaleiro", ["Guardian"] = "Guardião",
            ["Hunter"] = "Caçador", ["Assassin"] = "Assassino", ["Mercenary"] = "Mercenário", ["Scholar"] = "Estudioso",
            ["Cleric"] = "Clérigo", ["Mage"] = "Mago", ["Druid"] = "Druida", ["Cultist"] = "Cultista",
            ["Fiend"] = "Demônio", ["Royal"] = "Real", ["Graveguard"] = "Guarda tumular", ["Judicator"] = "Juiz",
            ["Helmet"] = "Capacete", ["Hood"] = "Capuz", ["Cowl"] = "Capuz", ["Armor"] = "Armadura",
            ["Jacket"] = "Jaqueta", ["Robe"] = "Túnica", ["Boots"] = "Botas", ["Shoes"] = "Sapatos", ["Sandals"] = "Sandálias",
            ["Sword"] = "Espada", ["Swords"] = "Espadas", ["Axe"] = "Machado", ["Mace"] = "Maça", ["Hammer"] = "Martelo",
            ["Spear"] = "Lança", ["Bow"] = "Arco", ["Crossbow"] = "Besta", ["Staff"] = "Cajado", ["Gloves"] = "Luvas",
            ["Shield"] = "Escudo", ["Torch"] = "Tocha", ["Cape"] = "Capa", ["Bag"] = "Bolsa", ["Horse"] = "Cavalo",
            ["Ox"] = "Boi", ["Calf"] = "Filhote", ["Foal"] = "Potro", ["Seed"] = "Semente", ["Seeds"] = "Sementes",
            ["Ore"] = "Minério", ["Bar"] = "Barra", ["Logs"] = "Troncos", ["Planks"] = "Tábuas", ["Hide"] = "Couro cru",
            ["Leather"] = "Couro", ["Fiber"] = "Fibra", ["Cloth"] = "Tecido", ["Stone"] = "Pedra", ["Block"] = "Bloco",
            ["Potion"] = "Poção", ["Food"] = "Comida", ["Fish"] = "Peixe"
        };

        foreach (var entry in wordMap.OrderByDescending(x => x.Key.Length))
            value = Regex.Replace(value, $"\\b{Regex.Escape(entry.Key)}\\b", entry.Value, RegexOptions.IgnoreCase);

        value = Regex.Replace(value, "\\s+", " ").Trim();
        return value;
    }

    private static string StripResourceRarity(string name, string itemId)
    {
        if (!IsResourceItem(itemId))
            return name;

        foreach (var rarity in new[] { "Uncommon ", "Rare ", "Exceptional ", "Pristine " })
        {
            if (name.StartsWith(rarity, StringComparison.OrdinalIgnoreCase))
                return name[rarity.Length..];
        }

        return name;
    }

    private static bool IsResourceItem(string itemId)
    {
        var id = itemId.ToUpperInvariant();
        return id.Contains("_ORE") || id.Contains("_WOOD") || id.Contains("_ROCK") || id.Contains("_HIDE") || id.Contains("_FIBER")
               || id.Contains("_PLANKS") || id.Contains("_STONEBLOCK") || id.Contains("_METALBAR") || id.Contains("_LEATHER") || id.Contains("_CLOTH");
    }

    private static string ToTitlePt(string value) => CapitalizeFirst(value.Replace('_', ' ').ToLowerInvariant());
    private static string ToTitleEn(string value) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = Regex.Replace(value.Trim(), "\\s+", " ");
        return value.Length == 1 ? value.ToUpperInvariant() : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
