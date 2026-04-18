using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public class WallStateDto : SpriteStateDto
{
    public WallStateDto() { Type = SpriteType.Wall; }
}