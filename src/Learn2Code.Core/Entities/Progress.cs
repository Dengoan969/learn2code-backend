namespace Learn2Code.Core.Entities;

public class Progress
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid TaskId { get; set; }
    public bool Completed { get; set; }
    public int AttemptsCount { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User Student { get; set; } = null!;
    public EducationalTask Task { get; set; } = null!;
}