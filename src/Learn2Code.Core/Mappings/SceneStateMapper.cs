using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Mappings;

public static class SceneStateMapper
{
    public static SceneStateDto ToDto(SceneState model)
    {
        if (model == null) return null!;

        return new SceneStateDto
        {
            Sprites = model.Sprites.Select(SpriteStateMapper.ToDto).ToList()
        };
    }

    public static SceneState ToModel(SceneStateDto dto)
    {
        if (dto == null) return null!;

        return new SceneState
        {
            Sprites = dto.Sprites.Select(SpriteStateMapper.ToModel).ToList()
        };
    }
}