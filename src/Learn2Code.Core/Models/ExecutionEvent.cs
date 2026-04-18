namespace Learn2Code.Core.Models;

public class ExecutionEvent
{
    public int Step { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}