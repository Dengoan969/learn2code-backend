namespace Learn2Code.Core.DTOs;

public record CourseDto(
    Guid Id,
    string Title,
    string Description,
    Guid TeacherId,
    DateTime CreatedAt
);