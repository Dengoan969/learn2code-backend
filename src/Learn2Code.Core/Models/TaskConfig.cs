using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public class TaskConfig
{
    public int GridWidth { get; set; } = 20;
    public int GridHeight { get; set; } = 20;
    public double TolerancePx { get; set; } = 5.0;
    public double MinTraceRatio { get; set; } = 0.7;
    public CheckLevel Level { get; set; } = CheckLevel.Normal;
}