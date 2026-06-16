using System.Text.Json;
using System.Text.Json.Serialization;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new SpriteStateJsonConverter(),
            new SpriteStateDtoJsonConverter()
        }
    };
}