using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record CreateTaskDraftRequest(
    [Required(ErrorMessage = "LessonId is required")]
    Guid? LessonId,
    [Range(1, int.MaxValue, ErrorMessage = "Order must be positive")]
    int Order
);