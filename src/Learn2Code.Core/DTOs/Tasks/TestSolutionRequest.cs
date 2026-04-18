using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record TestSolutionRequest(
    [Required(ErrorMessage = "Code is required")]
    string Code,
    [Required(ErrorMessage = "InitialState is required")]
    SceneStateDto InitialState,
    [Required(ErrorMessage = "Config is required")]
    TaskConfigDto Config
);