namespace Learn2Code.Core.DTOs;

public record SubmissionRequest(
    string StudentId,
    string Language,
    string Code,
    string? BlocklyXml = null,
    Dictionary<string, BlockMapping>? BlockMap = null
);

public record BlockMapping(
    string BlockId,
    string Type
);