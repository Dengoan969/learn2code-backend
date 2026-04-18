namespace Learn2Code.Core.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CourseId { get; set; }
    public Guid TeacherId { get; set; } // Преподаватель, ведущий группу (может быть не владельцем курса)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Course Course { get; set; } = null!;
    public User Teacher { get; set; } = null!;
    public ICollection<GroupStudent> GroupStudents { get; set; } = new List<GroupStudent>();
}