using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public abstract class SpriteStateDto
{
    public SpriteType Type { get; set; }

    // Непрерывные координаты центра спрайта в пикселях
    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;

    // Размеры спрайта в пикселях
    public double Width { get; set; } = 50.0;
    public double Height { get; set; } = 50.0;

    public bool Visible { get; set; } = true;
}