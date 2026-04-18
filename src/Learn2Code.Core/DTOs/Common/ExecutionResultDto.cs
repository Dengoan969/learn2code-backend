namespace Learn2Code.Core.DTOs;

public class ExecutionResultDto
{
    public SceneStateDto FinalState { get; set; } = new();
    public ExecutionTraceDto Trace { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}