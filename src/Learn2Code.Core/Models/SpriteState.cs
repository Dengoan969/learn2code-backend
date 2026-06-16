using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public abstract class SpriteState
{
    public SpriteType Type { get; set; }

    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;

    public double Width { get; set; } = 50.0;
    public double Height { get; set; } = 50.0;

    public bool Visible { get; set; } = true;
}