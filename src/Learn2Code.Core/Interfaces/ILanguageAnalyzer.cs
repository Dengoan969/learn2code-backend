using Learn2Code.Core.Models;

namespace Learn2Code.Core.Interfaces;

public interface ILanguageAnalyzer
{
    bool Supports(string languageId);
    Task<NormalizedProgram> ExtractAsync(string code);
}