using System.Text.Json;
using Learn2Code.Core;

namespace Learn2Code.Tests;

public static class JsonComparisonHelper
{
    private static readonly JsonSerializerOptions _options = JsonOptions.Default;

    /// <summary>
    ///     Сравнивает два объекта через их JSON-сериализацию.
    ///     Полезно для сравнения объектов, которые содержат сложные графы объектов
    ///     или полиморфные коллекции.
    /// </summary>
    public static bool JsonEquals<T>(T a, T b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;

        var jsonA = JsonSerializer.Serialize(a, _options);
        var jsonB = JsonSerializer.Serialize(b, _options);

        return jsonA == jsonB;
    }

    /// <summary>
    ///     Создает предикат для использования в Moq Verify с сравнением через JSON.
    /// </summary>
    public static Func<T, bool> JsonEquals<T>(T expected)
    {
        return actual => JsonEquals(expected, actual);
    }
}