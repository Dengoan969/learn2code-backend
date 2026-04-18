using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record UpdateLessonRequest(
    string? Title,
    string? Description,
    [Range(1, int.MaxValue, ErrorMessage = "Order must be positive")]
    int? Order,
    Guid? CourseId
);