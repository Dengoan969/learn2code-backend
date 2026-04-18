namespace Learn2Code.Core.Entities;

public class Submission
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid TaskId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string BlocklyXml { get; set; } = string.Empty;
    public string BlockMapJson { get; set; } = string.Empty;
    public string Language { get; set; } = "python";
    public bool IsPassed { get; set; }
    public bool IsOptimal { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User Student { get; set; } = null!;
    public EducationalTask Task { get; set; } = null!;
}