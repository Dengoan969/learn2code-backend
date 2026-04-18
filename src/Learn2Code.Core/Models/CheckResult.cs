using Learn2Code.Core.Enums;

namespace Learn2Code.Core.Models;

public record CheckResult(
    bool IsPassed,
    bool IsOptimal,
    string Hint,
    List<CodeIssue> Issues,
    Dictionary<string, double> Metrics
);

public record CodeIssue(
    IssueType Type,
    string Message,
    Severity Severity,
    string? BlockId = null,
    int? Line = null
);