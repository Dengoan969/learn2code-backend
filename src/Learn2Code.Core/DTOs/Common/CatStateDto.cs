using System.Text.Json.Serialization;
using Learn2Code.Core.Enums;

namespace Learn2Code.Core.DTOs;

public class CatStateDto : SpriteStateDto
{
    [JsonConstructor]
    public CatStateDto() { Type = SpriteType.Cat; }
    public double Direction { get; set; } = 90.0;
    public string Costume { get; set; } = "default";
    public Dictionary<string, int> SaidTexts { get; set; } = new();
    public Dictionary<string, int> CollectedItems { get; set; } = new();
}