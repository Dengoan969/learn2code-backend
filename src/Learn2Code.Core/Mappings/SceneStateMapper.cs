using Learn2Code.Core.DTOs;

namespace Learn2Code.Core.Mappings;

public static class SceneStateMapper
{
    public static SceneStateDto ToDto(Models.SceneState model)
    {
        if (model == null) return null!;

        return new SceneStateDto
        {
            Sprites = model.Sprites.Select(SpriteStateMapper.ToDto).ToList()
        };
    }

    public static Models.SceneState ToModel(SceneStateDto dto)
    {
        if (dto == null) return null!;

        return new Models.SceneState
        {
            Sprites = dto.Sprites.Select(SpriteStateMapper.ToModel).ToList()
        };
    }
}