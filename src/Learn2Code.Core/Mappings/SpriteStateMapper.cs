using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Mappings;

public static class SpriteStateMapper
{
    public static SpriteStateDto ToDto(SpriteState model)
    {
        if (model == null) return null!;

        return model switch
        {
            CatState cat => new CatStateDto
            {
                Type = cat.Type,
                GridX = cat.GridX,
                GridY = cat.GridY,
                Visible = cat.Visible,
                Direction = cat.Direction,
                Costume = cat.Costume,
                SaidTexts = new Dictionary<string, int>(cat.SaidTexts),
                CollectedItems = new Dictionary<string, int>(cat.CollectedItems)
            },
            AppleState apple => new AppleStateDto
            {
                Type = apple.Type,
                GridX = apple.GridX,
                GridY = apple.GridY,
                Visible = apple.Visible
            },
            WallState wall => new WallStateDto
            {
                Type = wall.Type,
                GridX = wall.GridX,
                GridY = wall.GridY,
                Visible = wall.Visible
            },
            _ => throw new ArgumentException($"Unknown sprite type: {model.GetType().Name}")
        };
    }

    public static SpriteState ToModel(SpriteStateDto dto)
    {
        if (dto == null) return null!;

        return dto switch
        {
            CatStateDto cat => new CatState
            {
                Type = cat.Type,
                GridX = cat.GridX,
                GridY = cat.GridY,
                Visible = cat.Visible,
                Direction = cat.Direction,
                Costume = cat.Costume,
                SaidTexts = new Dictionary<string, int>(cat.SaidTexts),
                CollectedItems = new Dictionary<string, int>(cat.CollectedItems)
            },
            AppleStateDto apple => new AppleState
            {
                Type = apple.Type,
                GridX = apple.GridX,
                GridY = apple.GridY,
                Visible = apple.Visible
            },
            WallStateDto wall => new WallState
            {
                Type = wall.Type,
                GridX = wall.GridX,
                GridY = wall.GridY,
                Visible = wall.Visible
            },
            _ => throw new ArgumentException($"Unknown sprite DTO type: {dto.GetType().Name}")
        };
    }
}