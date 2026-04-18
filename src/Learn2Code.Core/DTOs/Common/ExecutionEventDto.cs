namespace Learn2Code.Core.DTOs;

public class ExecutionEventDto
{
    public int Step { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}