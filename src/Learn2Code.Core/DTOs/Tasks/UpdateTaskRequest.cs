using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    [Range(1, int.MaxValue, ErrorMessage = "Order must be positive")]
    int? Order,
    TaskConfigDto? Config,
    SceneStateDto? InitialState,
    string? SolutionCode
);