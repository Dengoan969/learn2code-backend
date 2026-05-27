using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public abstract class SpriteState
{
    public SpriteType Type { get; set; }

    // ЗАМЕНА: GridX, GridY → X, Y (double)
    public double X { get; set; } = 0.0; // Пиксели, центр спрайта
    public double Y { get; set; } = 0.0; // Пиксели, центр спрайта

    // НОВОЕ: Размеры спрайта
    public double Width { get; set; } = 50.0; // Ширина в пикселях
    public double Height { get; set; } = 50.0; // Высота в пикселях

    public bool Visible { get; set; } = true;
}