using Learn2Code.Core.Models;

namespace Learn2Code.Core.Interfaces;

public interface IVerificationEngine
{
    ILanguageAnalyzer Analyzer { get; }

    CheckResult Compare(
        NormalizedProgram student,
        NormalizedProgram reference,
        ExecutionResult execution,
        SceneState expectedState,
        TaskConfig config);
}