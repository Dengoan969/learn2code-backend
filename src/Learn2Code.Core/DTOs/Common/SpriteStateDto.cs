using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

[JsonDerivedType(typeof(CatStateDto), typeDiscriminator: "cat")]
[JsonDerivedType(typeof(AppleStateDto), typeDiscriminator: "apple")]
[JsonDerivedType(typeof(WallStateDto), typeDiscriminator: "wall")]
public abstract class SpriteStateDto
{
    protected SpriteStateDto() { }
    
    public SpriteType Type { get; set; }
    public int GridX { get; set; } = 0;
    public int GridY { get; set; } = 0;
    public bool Visible { get; set; } = true;
}