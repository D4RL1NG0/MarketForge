using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlbionMarket.Application.Features.Market.Catalog;

/// <summary>
/// Conversor customizado que permite desserializar um valor que pode ser
/// um objeto único ou uma array, convertendo sempre para lista
/// </summary>
public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // É uma array, desserializa como tal
            var list = new List<T>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item != null)
                    list.Add(item);
            }
            return list;
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            // É um objeto único, desserializa e coloca em uma lista
            var item = JsonSerializer.Deserialize<T>(ref reader, options);
            var list = new List<T>();
            if (item != null)
                list.Add(item);
            return list;
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return new List<T>();
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
