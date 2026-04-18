namespace Learn2Code.Core.DTOs;

public class NormalizedProgramDto
{
    public List<CodeElementDto> Elements { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
}

public class CodeElementDto
{
    public string Type { get; set; } = string.Empty; // "Move", "Loop", "Condition", "Say"
    public string SemanticHint { get; set; } = string.Empty; // "move", "repeat", "if"
    public int? Line { get; set; }
    public string? BlockId { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}