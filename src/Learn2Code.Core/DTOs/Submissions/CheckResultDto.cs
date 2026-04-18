namespace Learn2Code.Core.DTOs;

public record CheckResultDto(
    bool IsPassed,
    bool IsOptimal,
    string Hint,
    List<CodeIssueDto> Issues,
    Dictionary<string, double> Metrics
);