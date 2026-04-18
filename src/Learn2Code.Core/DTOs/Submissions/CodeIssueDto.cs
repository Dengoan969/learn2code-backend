namespace Learn2Code.Core.DTOs;

public record CodeIssueDto(
    string Type,
    string Message,
    string Severity,
    string? BlockId,
    int? Line
);