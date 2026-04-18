namespace Learn2Code.Core.DTOs;

public record UpdateGroupRequest(
    string? Name,
    string? Description,
    Guid? TeacherId
);