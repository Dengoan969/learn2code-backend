using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record AddStudentToGroupRequest(
    [Required(ErrorMessage = "StudentId is required")]
    Guid StudentId
);