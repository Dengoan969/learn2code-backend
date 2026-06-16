using System.Text.Json;
using Learn2Code.Core;

namespace Learn2Code.Tests;

public static class JsonComparisonHelper
{
    private static readonly JsonSerializerOptions _options = JsonOptions.Default;

    public static bool JsonEquals<T>(T a, T b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        var jsonA = JsonSerializer.Serialize(a, _options);
        var jsonB = JsonSerializer.Serialize(b, _options);

        return jsonA == jsonB;
    }

    public static Func<T, bool> JsonEquals<T>(T expected)
    {
        return actual => JsonEquals(expected, actual);
    }
}