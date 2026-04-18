namespace Learn2Code.Core.DTOs;

public record SubmissionDto(
    Guid Id,
    Guid TaskId,
    string StudentId,
    string Code,
    string Language,
    bool IsPassed,
    bool IsOptimal,
    DateTime SubmittedAt,
    CheckResultDto? Result
);