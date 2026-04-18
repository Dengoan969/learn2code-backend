namespace Learn2Code.Core.Entities;

public class GroupStudent
{
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Group Group { get; set; } = null!;
    public User Student { get; set; } = null!;
}