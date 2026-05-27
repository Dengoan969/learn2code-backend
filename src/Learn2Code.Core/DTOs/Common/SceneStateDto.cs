using System.Text.Json.Serialization;

namespace Learn2Code.Core.DTOs;

public class SceneStateDto
{
    [JsonConstructor]
    public SceneStateDto()
    {
    }

    public SceneStateDto(params SpriteStateDto[] sprites)
    {
        Sprites = new List<SpriteStateDto>(sprites);
    }

    public List<SpriteStateDto> Sprites { get; set; } = new();
}