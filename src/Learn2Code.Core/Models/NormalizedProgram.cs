namespace Learn2Code.Core.Models;

public class NormalizedProgram
{
    public List<CodeElement> Elements { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
}

public class CodeElement
{
    public string Type { get; set; } = string.Empty;
    public string SemanticHint { get; set; } = string.Empty;
    public int? Line { get; set; }
    public string? BlockId { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}