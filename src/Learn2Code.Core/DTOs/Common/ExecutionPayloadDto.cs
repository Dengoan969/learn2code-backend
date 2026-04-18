namespace Learn2Code.Core.DTOs;

public record ExecutionPayloadDto(
    bool Success,
    string? Error,
    string StateJson,
    string TraceJson,
    string AstNormalizedJson
);