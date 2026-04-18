namespace Learn2Code.Core.DTOs;

public record GroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid CourseId,
    Guid TeacherId,
    DateTime CreatedAt,
    List<UserDto> Students
);