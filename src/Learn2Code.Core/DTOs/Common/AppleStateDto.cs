using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public class AppleStateDto : SpriteStateDto
{
    [JsonConstructor]
    public AppleStateDto()
    {
        Type = SpriteType.Apple;
    }
}