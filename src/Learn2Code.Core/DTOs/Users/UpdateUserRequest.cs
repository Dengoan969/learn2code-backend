namespace Learn2Code.Core.DTOs;

public record UpdateUserRequest(
    string? Login,
    string? DisplayName,
    string? Role
);