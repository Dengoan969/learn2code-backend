using System.Text.Json;
using System.Text.Json.Serialization;
using Learn2Code.Core.Models;

namespace Learn2Code.Core;

/// <summary>
/// Provides centralized JsonSerializerOptions for the entire application.
/// Ensures consistent JSON serialization/deserialization across API, EF Core, and tests.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Universal JsonSerializerOptions with camelCase naming, case-insensitive property matching,
    /// enum string conversion, and polymorphic serialization via JsonDerivedType attributes.
    /// Used everywhere: API controllers, EF Core JSON columns, tests, and other serialization.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new SpriteStateJsonConverter()
        }
    };
}