using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record CreateUserRequest(
    [Required(ErrorMessage = "Login is required")]
    string Login,
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    string Password,
    [Required(ErrorMessage = "DisplayName is required")]
    string DisplayName,
    [Required(ErrorMessage = "Role is required")]
    string Role
);