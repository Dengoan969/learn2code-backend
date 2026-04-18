using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record ResetPasswordRequest(
    [Required(ErrorMessage = "New password is required")]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters")]
    string NewPassword
);