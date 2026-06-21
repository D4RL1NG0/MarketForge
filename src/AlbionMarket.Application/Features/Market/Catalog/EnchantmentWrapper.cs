using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AlbionMarket.Application.Features.Market.Catalog;

public class AodEnchantmentWrapper
{
    [JsonConverter(typeof(SingleOrArrayConverter<AodEnchantmentRaw>))]
    public List<AodEnchantmentRaw>? Enchantment { get; set; }
}