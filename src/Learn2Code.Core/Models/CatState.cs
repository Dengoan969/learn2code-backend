using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public class CatState : SpriteState
{
    public CatState()
    {
        Type = SpriteType.Cat;
    }

    public double Direction { get; set; } = 90.0;
    public string Costume { get; set; } = "default";
    public Dictionary<string, int> SaidTexts { get; set; } = new();
    public Dictionary<string, int> CollectedItems { get; set; } = new();
}