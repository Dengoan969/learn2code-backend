using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public class WallStateDto : SpriteStateDto
{
    [JsonConstructor]
    public WallStateDto() { Type = SpriteType.Wall; }
}