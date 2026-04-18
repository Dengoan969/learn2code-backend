using Learn2Code.Core.Models;

namespace Learn2Code.Core.Entities;

public class EducationalTask
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public Guid LessonId { get; set; }
    
    public TaskPipelineState PipelineState { get; set; } = TaskPipelineState.Draft;
    public SceneState InitialState { get; set; } = new();
    public SceneState? ExpectedFinalState { get; set; }
    public string? SolutionCode { get; set; }
    public ExecutionTrace? SolutionTrace { get; set; }
    public TaskConfig Config { get; set; } = new();
    
    public Lesson Lesson { get; set; } = null!;
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}

public enum TaskPipelineState
{
    Draft,
    Published
}