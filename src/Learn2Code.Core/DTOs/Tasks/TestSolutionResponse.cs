namespace Learn2Code.Core.DTOs;

public record TestSolutionResponse(
    SceneStateDto FinalState,
    bool Success,
    string? Error
);