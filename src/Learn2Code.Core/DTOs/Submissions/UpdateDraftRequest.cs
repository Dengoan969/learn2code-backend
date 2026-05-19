namespace Learn2Code.Core.DTOs;

public record UpdateDraftRequest(
    string Code,
    string? BlocklyXml = null
);