using System.Text.Json;
using Learn2Code.Core;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
public class SpriteStateDtoJsonConverterTests
{
    [SetUp]
    public void SetUp()
    {
        _options = JsonOptions.Default;
    }

    private JsonSerializerOptions _options;

    [Test]
    public void Serialize_Deserialize_CatStateDto_RoundTrip()
    {
        var original = new CatStateDto
        {
            X = 500.0,
            Y = 1000.0,
            Width = 50.0,
            Height = 50.0,
            Visible = true,
            Direction = 45.0,
            Costume = "cat_costume",
            SaidTexts = new Dictionary<string, int> { { "Meow!", 1 } },
            CollectedItems = new Dictionary<string, int> { { "apple", 2 } }
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<SpriteStateDto>(json, _options);

        Assert.That(deserialized, Is.InstanceOf<CatStateDto>());
        var cat = (CatStateDto)deserialized!;
        Assert.That(cat.X, Is.EqualTo(original.X));
        Assert.That(cat.Y, Is.EqualTo(original.Y));
        Assert.That(cat.Width, Is.EqualTo(original.Width));
        Assert.That(cat.Height, Is.EqualTo(original.Height));
        Assert.That(cat.Visible, Is.EqualTo(original.Visible));
        Assert.That(cat.Direction, Is.EqualTo(original.Direction));
        Assert.That(cat.Costume, Is.EqualTo(original.Costume));
        Assert.That(cat.SaidTexts, Has.Count.EqualTo(1));
        Assert.That(cat.SaidTexts["Meow!"], Is.EqualTo(1));
        Assert.That(cat.CollectedItems, Has.Count.EqualTo(1));
        Assert.That(cat.CollectedItems["apple"], Is.EqualTo(2));
    }

    [Test]
    public void Serialize_Deserialize_AppleStateDto_RoundTrip()
    {
        var original = new AppleStateDto
        {
            X = 250.0,
            Y = 750.0,
            Width = 50.0,
            Height = 50.0,
            Visible = false
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<SpriteStateDto>(json, _options);

        Assert.That(deserialized, Is.InstanceOf<AppleStateDto>());
        var apple = (AppleStateDto)deserialized!;
        Assert.That(apple.X, Is.EqualTo(original.X));
        Assert.That(apple.Y, Is.EqualTo(original.Y));
        Assert.That(apple.Width, Is.EqualTo(original.Width));
        Assert.That(apple.Height, Is.EqualTo(original.Height));
        Assert.That(apple.Visible, Is.EqualTo(original.Visible));
    }

    [Test]
    public void Serialize_Deserialize_WallStateDto_RoundTrip()
    {
        var original = new WallStateDto
        {
            X = 0.0,
            Y = 0.0,
            Width = 50.0,
            Height = 50.0,
            Visible = true
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<SpriteStateDto>(json, _options);

        Assert.That(deserialized, Is.InstanceOf<WallStateDto>());
        var wall = (WallStateDto)deserialized!;
        Assert.That(wall.X, Is.EqualTo(original.X));
        Assert.That(wall.Y, Is.EqualTo(original.Y));
        Assert.That(wall.Width, Is.EqualTo(original.Width));
        Assert.That(wall.Height, Is.EqualTo(original.Height));
        Assert.That(wall.Visible, Is.EqualTo(original.Visible));
    }

    [Test]
    public void Deserialize_WithTypeField_CorrectlyIdentifiesSpriteType()
    {
        var catJson = @"{
            ""type"": ""Cat"",
            ""gridX"": 10,
            ""gridY"": 20,
            ""visible"": true,
            ""direction"": 45.0,
            ""costume"": ""cat_costume"",
            ""saidTexts"": { ""Meow!"": 1 },
            ""collectedItems"": { ""apple"": 2 }
        }";

        var appleJson = @"{
            ""type"": ""Apple"",
            ""gridX"": 5,
            ""gridY"": 15,
            ""visible"": false
        }";

        var wallJson = @"{
            ""type"": ""Wall"",
            ""gridX"": 0,
            ""gridY"": 0,
            ""visible"": true
        }";

        var cat = JsonSerializer.Deserialize<SpriteStateDto>(catJson, _options);
        Assert.That(cat, Is.InstanceOf<CatStateDto>());

        var apple = JsonSerializer.Deserialize<SpriteStateDto>(appleJson, _options);
        Assert.That(apple, Is.InstanceOf<AppleStateDto>());

        var wall = JsonSerializer.Deserialize<SpriteStateDto>(wallJson, _options);
        Assert.That(wall, Is.InstanceOf<WallStateDto>());
    }

    [Test]
    public void Deserialize_MissingTypeField_ThrowsJsonException()
    {
        var json = @"{
            ""gridX"": 10,
            ""gridY"": 20,
            ""visible"": true
        }";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<SpriteStateDto>(json, _options));
    }

    [Test]
    public void Deserialize_InvalidTypeField_ThrowsJsonException()
    {
        var json = @"{
            ""type"": ""InvalidSprite"",
            ""gridX"": 10,
            ""gridY"": 20
        }";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<SpriteStateDto>(json, _options));
    }

    [Test]
    public void Serialize_IncludesTypeField()
    {
        var cat = new CatStateDto { X = 10, Y = 20 };

        var json = JsonSerializer.Serialize(cat, _options);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.TryGetProperty("type", out var typeProperty), Is.True);
        Assert.That(typeProperty.GetString(), Is.EqualTo("cat"));
    }

    [Test]
    public void SceneStateDto_WithMultipleSpriteTypes_SerializesAndDeserializes()
    {
        var scene = new SceneStateDto(
            new CatStateDto { X = 10, Y = 20 },
            new AppleStateDto { X = 30, Y = 40 },
            new WallStateDto { X = 50, Y = 60 }
        );

        var json = JsonSerializer.Serialize(scene, _options);
        var deserialized = JsonSerializer.Deserialize<SceneStateDto>(json, _options);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.Sprites, Has.Count.EqualTo(3));
        Assert.That(deserialized.Sprites[0], Is.InstanceOf<CatStateDto>());
        Assert.That(deserialized.Sprites[1], Is.InstanceOf<AppleStateDto>());
        Assert.That(deserialized.Sprites[2], Is.InstanceOf<WallStateDto>());
    }
}