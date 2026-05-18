using System.Text.Json.Serialization;

namespace Learn2Code.Core.DTOs;

public class SceneStateDto
{
    public List<SpriteStateDto> Sprites { get; set; } = new();

    [JsonConstructor]
    public SceneStateDto() { }

    public SceneStateDto(params SpriteStateDto[] sprites)
    {
        Sprites = new List<SpriteStateDto>(sprites);
    }
}