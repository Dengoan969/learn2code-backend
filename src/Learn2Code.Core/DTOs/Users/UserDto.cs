namespace Learn2Code.Core.DTOs;

public record UserDto(
    Guid Id,
    string Login,
    string DisplayName,
    string Role,
    DateTime CreatedAt
);