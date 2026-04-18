namespace Learn2Code.Core.DTOs;

public record LoginResponse(
    string Token,
    UserDto User
);