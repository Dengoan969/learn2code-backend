using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public class TaskConfig
{
    public double SceneWidth { get; set; } = 1000.0;
    public double SceneHeight { get; set; } = 1000.0;

    public double TolerancePx { get; set; } = 5.0;
    public double MinTraceRatio { get; set; } = 0.7;
    public CheckLevel Level { get; set; } = CheckLevel.Normal;
}