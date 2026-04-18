using System.Text.Json;
using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

/// <summary>
/// Custom JSON converter for SpriteState that handles polymorphic deserialization
/// based on the "type" field in JSON (matching Python sandbox output).
/// </summary>
public class SpriteStateJsonConverter : JsonConverter<SpriteState>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(SpriteState).IsAssignableFrom(typeToConvert);

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

        // Parse the type string to SpriteType enum
        if (!Enum.TryParse<SpriteType>(typeStr, ignoreCase: true, out var spriteType))
            throw new JsonException($"Unknown sprite type: {typeStr}");

        // Create new options without this converter to avoid recursion
        var optionsWithoutConverter = new JsonSerializerOptions
        {
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            ReferenceHandler = options.ReferenceHandler,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            WriteIndented = options.WriteIndented
        };
        
        // Copy all converters except SpriteStateJsonConverter
        foreach (var converter in options.Converters)
        {
            if (converter is not SpriteStateJsonConverter)
                optionsWithoutConverter.Converters.Add(converter);
        }

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
        // Create new options without this converter to avoid recursion
        var optionsWithoutConverter = new JsonSerializerOptions
        {
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            ReferenceHandler = options.ReferenceHandler,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            WriteIndented = options.WriteIndented
        };
        
        // Copy all converters except SpriteStateJsonConverter
        foreach (var converter in options.Converters)
        {
            if (converter is not SpriteStateJsonConverter)
                optionsWithoutConverter.Converters.Add(converter);
        }

        // Use default serialization for writing
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutConverter);
    }
}