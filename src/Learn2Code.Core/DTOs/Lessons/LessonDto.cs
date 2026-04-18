namespace Learn2Code.Core.DTOs;

public record LessonDto(
    Guid Id,
    string Title,
    string Description,
    int Order,
    Guid CourseId
);