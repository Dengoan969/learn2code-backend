namespace Learn2Code.Core.Models;

public record ExecutionPayload(
    bool Success,
    string? Error,
    string StateJson,
    string TraceJson,
    string AstNormalizedJson
);