namespace Learn2Code.Core.DTOs;

public record SubmissionDto(
    Guid Id,
    Guid TaskId,
    string StudentId,
    string Code,
    DateTime SubmittedAt,
    CheckResultDto? Result,
    bool IsDraft = false
);