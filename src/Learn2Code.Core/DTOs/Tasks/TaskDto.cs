using Learn2Code.Core.Entities;

namespace Learn2Code.Core.DTOs;

public record TaskDto(
    Guid Id,
    string Title,
    string Description,
    int Order,
    Guid LessonId,
    TaskPipelineState PipelineState,
    SceneStateDto InitialState,
    SceneStateDto? ExpectedFinalState,
    ExecutionTraceDto? SolutionTrace,
    TaskConfigDto Config,
    string? SolutionCode
);