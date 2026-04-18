using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record LoginRequest(
    [Required(ErrorMessage = "Login is required")]
    string Login,
    [Required(ErrorMessage = "Password is required")]
    string Password
);