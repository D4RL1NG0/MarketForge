using System.Text.Json.Serialization;

namespace AlbionMarket.Mobile.Models;

public sealed class CatalogCategory
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("groups")]
    public List<CatalogGroup> Groups { get; set; } = new();

    public string DisplayName => Count > 0 ? $"{Name} ({Count})" : Name;
}

public sealed class CatalogGroup
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    public string DisplayName => Count > 0 ? $"{Name} ({Count})" : Name;
}
