namespace Learn2Code.Core.DTOs;

public record ProgressDto(
    Guid TaskId,
    string TaskTitle,
    bool Completed,
    int AttemptsCount,
    DateTime LastAttemptAt
);