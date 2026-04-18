namespace Learn2Code.Core.Entities;

public class Lesson
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public Guid CourseId { get; set; }

    // Navigation
    public Course Course { get; set; } = null!;
    public ICollection<EducationalTask> Tasks { get; set; } = new List<EducationalTask>();
}