using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record CreateGroupRequest(
    [Required(ErrorMessage = "Name is required")]
    string Name,
    string? Description,
    [Required(ErrorMessage = "CourseId is required")]
    Guid CourseId,
    Guid TeacherId
);