using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public abstract class SpriteStateDto
{
    public SpriteStateDto() { }
    
    public SpriteType Type { get; set; }
    public int GridX { get; set; } = 0;
    public int GridY { get; set; } = 0;
    public bool Visible { get; set; } = true;
}