namespace Learn2Code.Core.Models;

public class ExecutionResult
{
    public SceneState FinalState { get; set; } = new();
    public ExecutionTrace Trace { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}