using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

/// <summary>
/// Elegant JSON converter for SpriteStateDto that handles polymorphic deserialization
/// based on the "type" field in JSON (matching Python sandbox output).
/// </summary>
public class SpriteStateDtoJsonConverter : JsonConverter<SpriteStateDto>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(SpriteStateDto).IsAssignableFrom(typeToConvert);

    public override SpriteStateDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
            throw new JsonException("Missing 'type' property in sprite JSON");

        SpriteType spriteType;
        
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            var typeStr = typeElement.GetString();
            
            if (string.IsNullOrEmpty(typeStr))
                throw new JsonException("Sprite type is null or empty");

            if (!Enum.TryParse<SpriteType>(typeStr, ignoreCase: true, out spriteType))
                throw new JsonException($"Unknown sprite type: {typeStr}");
        }
        else if (typeElement.ValueKind == JsonValueKind.Number)
        {
            if (!typeElement.TryGetInt32(out var typeInt))
                throw new JsonException($"Invalid numeric sprite type: {typeElement.GetRawText()}");
            
            if (!Enum.IsDefined(typeof(SpriteType), typeInt))
                throw new JsonException($"Unknown sprite type value: {typeInt}");
                
            spriteType = (SpriteType)typeInt;
        }
        else
        {
            throw new JsonException($"Invalid 'type' property type: {typeElement.ValueKind}. Expected string or number.");
        }
        
        var optionsWithoutSelf = CreateOptionsWithoutSelf(options);

        SpriteStateDto? result = spriteType switch
        {
            SpriteType.Cat => JsonSerializer.Deserialize<CatStateDto>(root.GetRawText(), optionsWithoutSelf),
            SpriteType.Apple => JsonSerializer.Deserialize<AppleStateDto>(root.GetRawText(), optionsWithoutSelf),
            SpriteType.Wall => JsonSerializer.Deserialize<WallStateDto>(root.GetRawText(), optionsWithoutSelf),
            _ => throw new JsonException($"Unsupported sprite type: {spriteType}")
        };
        
        if (result == null)
            throw new JsonException($"Failed to deserialize sprite of type {spriteType}");
        
        return result;
    }

    public override void Write(Utf8JsonWriter writer, SpriteStateDto value, JsonSerializerOptions options)
    {
        var optionsWithoutSelf = CreateOptionsWithoutSelf(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), optionsWithoutSelf);
    }

    private static JsonSerializerOptions CreateOptionsWithoutSelf(JsonSerializerOptions original)
    {
        var options = new JsonSerializerOptions(original);
        
        // Remove this converter to avoid infinite recursion
        for (int i = options.Converters.Count - 1; i >= 0; i--)
        {
            if (options.Converters[i] is SpriteStateDtoJsonConverter)
            {
                options.Converters.RemoveAt(i);
                break;
            }
        }
        
        return options;
    }
}