using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record CreateCourseRequest(
    [Required(ErrorMessage = "Title is required")]
    string Title,
    string Description
);