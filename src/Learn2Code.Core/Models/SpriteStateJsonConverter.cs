using System.Text.Json;
using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public class SpriteStateJsonConverter : JsonConverter<SpriteState>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(SpriteState).IsAssignableFrom(typeToConvert);
    }

    public override SpriteState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
            throw new JsonException("Missing 'type' property in sprite JSON");

        var typeStr = typeElement.GetString();
        if (string.IsNullOrEmpty(typeStr))
            throw new JsonException("Sprite type is null or empty");

        if (!Enum.TryParse<SpriteType>(typeStr, true, out var spriteType))
            throw new JsonException($"Unknown sprite type: {typeStr}");

        var optionsWithoutConverter = new JsonSerializerOptions
        {
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            ReferenceHandler = options.ReferenceHandler,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            WriteIndented = options.WriteIndented
        };

        foreach (var converter in options.Converters)
            if (converter is not SpriteStateJsonConverter)
                optionsWithoutConverter.Converters.Add(converter);

        SpriteState? result = spriteType switch
        {
            SpriteType.Cat => JsonSerializer.Deserialize<CatState>(root.GetRawText(), optionsWithoutConverter),
            SpriteType.Apple => JsonSerializer.Deserialize<AppleState>(root.GetRawText(), optionsWithoutConverter),
            SpriteType.Wall => JsonSerializer.Deserialize<WallState>(root.GetRawText(), optionsWithoutConverter),
            _ => throw new JsonException($"Unsupported sprite type: {spriteType}")
        };

        return result ?? throw new JsonException($"Failed to deserialize sprite of type {spriteType}");
    }

    public override void Write(Utf8JsonWriter writer, SpriteState value, JsonSerializerOptions options)
    {
        var optionsWithoutConverter = new JsonSerializerOptions
        {
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            ReferenceHandler = options.ReferenceHandler,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            WriteIndented = options.WriteIndented
        };

        foreach (var converter in options.Converters)
            if (converter is not SpriteStateJsonConverter)
                optionsWithoutConverter.Converters.Add(converter);

        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }
}