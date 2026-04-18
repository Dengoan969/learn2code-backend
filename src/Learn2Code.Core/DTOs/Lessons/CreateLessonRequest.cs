using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record CreateLessonRequest(
    [Required(ErrorMessage = "Title is required")]
    string Title,
    string Description,
    [Range(1, int.MaxValue, ErrorMessage = "Order must be positive")]
    int Order,
    [Required(ErrorMessage = "CourseId is required")]
    Guid CourseId
);