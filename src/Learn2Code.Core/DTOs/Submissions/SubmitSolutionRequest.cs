using System.ComponentModel.DataAnnotations;

namespace Learn2Code.Core.DTOs;

public record SubmitSolutionRequest(
    [Required(ErrorMessage = "Language is required")]
    string Language,
    [Required(ErrorMessage = "Code is required")]
    string Code,
    string? BlocklyXml,
    Dictionary<string, BlockMapping>? BlockMap
);