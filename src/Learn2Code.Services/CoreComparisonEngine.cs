using Learn2Code.Core.Enums;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;

namespace Learn2Code.Services;

public class CoreComparisonEngine : IVerificationEngine
{
    private readonly StateComparer _stateComparer;

    public CoreComparisonEngine(ILanguageAnalyzer analyzer)
    {
        Analyzer = analyzer;
        _stateComparer = new StateComparer();
    }

    public ILanguageAnalyzer Analyzer { get; }

    public CheckResult Compare(
        NormalizedProgram student,
        NormalizedProgram reference,
        ExecutionResult execution,
        SceneState expectedState,
        TaskConfig config,
        ExecutionTrace solutionTrace)
    {
        if (!execution.Success)
        {
            var errorIssues = new List<CodeIssue>
            {
                new(
                    IssueType.SyntaxError,
                    $"Ошибка выполнения: {execution.Error}",
                    Severity.Error,
                    null,
                    1)
            };

            return new CheckResult(
                false,
                false,
                "Код содержит ошибку выполнения",
                errorIssues,
                new Dictionary<string, double>
                {
                    { "StateScore", 0.0 },
                    { "TraceSimilarity", 0.0 },
                    { "AstSimilarity", 0.0 },
                    { "ParameterSimilarity", 0.0 },
                    { "RedundantCount", 0.0 },
                    { "MissingCount", 0.0 }
                },
                execution.FinalState
            );
        }

        var stateResult = CheckState(execution.FinalState, expectedState, config, execution.Trace, solutionTrace);

        var traceSimilarity = solutionTrace != null
            ? CalculateTraceSimilarityWithSolutionTrace(execution.Trace, solutionTrace)
            : 0.0;

        var astMetrics = CalculateAstSimilarity(student, reference);

        var isPassed = stateResult.IsPassed;
        var isOptimal = traceSimilarity >= config.MinTraceRatio
                        && astMetrics["RedundantCount"] == 0
                        && (!astMetrics.TryGetValue("ParameterMismatchCount", out var mismatchCount) ||
                            mismatchCount == 0);

        var issues = BuildIssues(stateResult, traceSimilarity, astMetrics, student, config);
        var hint = GenerateHint(stateResult, traceSimilarity, astMetrics, isPassed, isOptimal);

        var metrics = new Dictionary<string, double>
        {
            { "StateScore", stateResult.IsPassed ? 1.0 : 0.0 },
            { "TraceSimilarity", traceSimilarity },
            { "AstSimilarity", astMetrics.GetValueOrDefault("SemanticSimilarity", 0) },
            { "ParameterSimilarity", astMetrics.GetValueOrDefault("ParameterSimilarity", 1.0) },
            { "RedundantCount", astMetrics.GetValueOrDefault("RedundantCount", 0) },
            { "MissingCount", astMetrics.GetValueOrDefault("MissingCount", 0) }
        };

        return new CheckResult(
            isPassed,
            isOptimal,
            hint,
            issues,
            metrics,
            execution.FinalState
        );
    }

    private StateCheckResult CheckState(
        SceneState actualState,
        SceneState expectedState,
        TaskConfig config,
        ExecutionTrace? studentTrace = null,
        ExecutionTrace? solutionTrace = null)
    {
        if (actualState == null)
            return new StateCheckResult(false, "Не удалось получить состояние сцены");

        if (expectedState == null)
            return new StateCheckResult(false, "Отсутствует ожидаемое состояние сцены");

        try
        {
            Console.WriteLine($"CheckState: actualState.Sprites.Count = {actualState.Sprites.Count}");
            Console.WriteLine($"CheckState: expectedState.Sprites.Count = {expectedState.Sprites.Count}");

            if (actualState.Sprites.Count > 0 && expectedState.Sprites.Count > 0)
            {
                var actualCat = actualState.Sprites.OfType<CatState>().FirstOrDefault();
                var expectedCat = expectedState.Sprites.OfType<CatState>().FirstOrDefault();

                if (actualCat != null && expectedCat != null)
                {
                    Console.WriteLine(
                        $"CheckState: actualCat at ({actualCat.X}, {actualCat.Y}), direction {actualCat.Direction}");
                    Console.WriteLine(
                        $"CheckState: expectedCat at ({expectedCat.X}, {expectedCat.Y}), direction {expectedCat.Direction}");
                    Console.WriteLine(
                        $"CheckState: actualCat.Costume = {actualCat.Costume}, expectedCat.Costume = {expectedCat.Costume}");
                    Console.WriteLine(
                        $"CheckState: actualCat.Visible = {actualCat.Visible}, expectedCat.Visible = {expectedCat.Visible}");
                }
            }

            var isEqual = _stateComparer.Compare(actualState, expectedState, config.TolerancePx);

            if (!isEqual) return new StateCheckResult(false, "Состояние сцены не соответствует ожидаемому");

            if (studentTrace != null && solutionTrace != null)
            {
                var sayEventsMatch = _stateComparer.CompareSayEvents(studentTrace, solutionTrace, config.TolerancePx);
                if (!sayEventsMatch)
                    return new StateCheckResult(false, "Текст сказан не в том месте");
            }

            return new StateCheckResult(true, null);
        }
        catch (Exception ex)
        {
            return new StateCheckResult(false, $"Ошибка проверки состояния: {ex.Message}");
        }
    }

    private double CalculateTraceSimilarity(ExecutionTrace trace, List<CodeElement> referenceElements)
    {
        if (trace == null || trace.Events == null)
            return 0.0;

        var actualTrace = trace.Events
            .Select(e => e.EventType)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var expectedTrace = referenceElements
            .Select(e => !string.IsNullOrEmpty(e.SemanticHint) ? e.SemanticHint : e.Type)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        if (expectedTrace.Count == 0)
            return 1.0;

        if (actualTrace.Count == 0)
            return 0.0;

        var lcsLength = CalculateLCS(actualTrace, expectedTrace);
        var similarity = (double)lcsLength / Math.Max(expectedTrace.Count, actualTrace.Count);
        return Math.Round(similarity, 2);
    }

    private double CalculateTraceSimilarityWithSolutionTrace(ExecutionTrace studentTrace, ExecutionTrace solutionTrace)
    {
        if (studentTrace == null || studentTrace.Events == null)
            return 0.0;

        if (solutionTrace == null || solutionTrace.Events == null)
            return 0.0;

        var studentEvents = studentTrace.Events
            .Select(e => e.EventType)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var solutionEvents = solutionTrace.Events
            .Select(e => e.EventType)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        if (solutionEvents.Count == 0)
            return 1.0;

        if (studentEvents.Count == 0)
            return 0.0;

        var lcsLength = CalculateLCS(studentEvents, solutionEvents);
        var similarity = (double)lcsLength / Math.Max(solutionEvents.Count, studentEvents.Count);
        return Math.Round(similarity, 2);
    }

    private int CalculateLCS(List<string> seq1, List<string> seq2)
    {
        var m = seq1.Count;
        var n = seq2.Count;
        var dp = new int[m + 1, n + 1];

        for (var i = 1; i <= m; i++)
        for (var j = 1; j <= n; j++)
            if (seq1[i - 1] == seq2[j - 1])
                dp[i, j] = dp[i - 1, j - 1] + 1;
            else
                dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);

        return dp[m, n];
    }

    private Dictionary<string, double> CalculateAstSimilarity(NormalizedProgram student, NormalizedProgram reference)
    {
        var studentTypes = student.Elements.Select(e => e.Type).ToList();
        var referenceTypes = reference.Elements.Select(e => e.Type).ToList();

        var commonCount = studentTypes.Intersect(referenceTypes).Count();
        var structuralSimilarity = referenceTypes.Count > 0
            ? (double)commonCount / referenceTypes.Count
            : 1.0;

        var redundantCount = Math.Max(0, studentTypes.Count - referenceTypes.Count);

        var semanticMatches = 0;
        var parameterMismatches = 0;
        var matchedStudentIndices = new HashSet<int>();

        foreach (var refElement in reference.Elements)
        {
            var bestMatchIndex = -1;
            var bestMatchScore = 0.0;

            for (var i = 0; i < student.Elements.Count; i++)
            {
                if (matchedStudentIndices.Contains(i))
                    continue;

                var studentElement = student.Elements[i];
                var score = CalculateElementMatchScore(studentElement, refElement);
                if (score > bestMatchScore)
                {
                    bestMatchScore = score;
                    bestMatchIndex = i;
                }
            }

            if (bestMatchIndex >= 0 && bestMatchScore >= 0.5)
            {
                matchedStudentIndices.Add(bestMatchIndex);
                semanticMatches++;

                if (bestMatchScore < 1.0)
                    parameterMismatches++;
            }
        }

        var semanticSimilarity = reference.Elements.Count > 0
            ? (double)semanticMatches / reference.Elements.Count
            : 1.0;

        var parameterSimilarity = reference.Elements.Count > 0
            ? 1.0 - (double)parameterMismatches / reference.Elements.Count
            : 1.0;

        var missingCount = reference.Elements.Count - semanticMatches;

        return new Dictionary<string, double>
        {
            { "StructuralSimilarity", Math.Round(structuralSimilarity, 2) },
            { "SemanticSimilarity", Math.Round(semanticSimilarity, 2) },
            { "ParameterSimilarity", Math.Round(parameterSimilarity, 2) },
            { "ParameterMismatchCount", parameterMismatches },
            { "RedundantCount", redundantCount },
            { "MissingCount", missingCount }
        };
    }

    private double CalculateElementMatchScore(CodeElement student, CodeElement reference)
    {
        if (student.Type != reference.Type)
            return 0.0;

        if (student.SemanticHint != reference.SemanticHint)
            return 0.3;

        var paramScore = CalculateParameterSimilarity(student.Parameters, reference.Parameters);
        return 0.7 + 0.3 * paramScore;
    }

    private double CalculateParameterSimilarity(
        Dictionary<string, object?> studentParams,
        Dictionary<string, object?> referenceParams)
    {
        if (referenceParams.Count == 0)
            return 1.0;

        var matched = 0;
        foreach (var (key, refValue) in referenceParams)
            if (studentParams.TryGetValue(key, out var studentValue))
            {
                if (Equals(refValue, studentValue))
                {
                    matched++;
                }
                else if (IsNumeric(refValue) && IsNumeric(studentValue))
                {
                    var refNum = Convert.ToDouble(refValue);
                    var stuNum = Convert.ToDouble(studentValue);
                    if (Math.Abs(refNum - stuNum) < 0.001)
                        matched++;
                }
            }

        return (double)matched / referenceParams.Count;
    }

    private bool IsNumeric(object? value)
    {
        return value is int or long or float or double or decimal;
    }

    private List<CodeIssue> BuildIssues(
        StateCheckResult stateResult,
        double traceSimilarity,
        Dictionary<string, double> astMetrics,
        NormalizedProgram studentProgram,
        TaskConfig config)
    {
        var issues = new List<CodeIssue>();

        if (!stateResult.IsPassed && !string.IsNullOrEmpty(stateResult.ErrorMessage))
            issues.Add(new CodeIssue(
                IssueType.StateMismatch,
                stateResult.ErrorMessage,
                Severity.Error
            ));

        if (traceSimilarity < config.MinTraceRatio)
            issues.Add(new CodeIssue(
                IssueType.TraceMismatch,
                $"Последовательность действий отличается от оптимальной (сходство: {traceSimilarity:P0})",
                Severity.Warning
            ));

        if (astMetrics.TryGetValue("RedundantCount", out var redundant) && redundant > 0)
            issues.Add(new CodeIssue(
                IssueType.RedundantCode,
                $"Обнаружены лишние действия ({redundant}). Попробуйте оптимизировать решение.",
                Severity.Warning
            ));

        if (astMetrics.TryGetValue("MissingCount", out var missing) && missing > 0)
            issues.Add(new CodeIssue(
                IssueType.MissingElement,
                $"Не хватает {missing} действий. Проверь, все ли необходимые шаги выполнены.",
                Severity.Warning
            ));

        if (astMetrics.TryGetValue("SemanticSimilarity", out var semantic) && semantic < 0.7)
            issues.Add(new CodeIssue(
                IssueType.SemanticMismatch,
                "Код содержит не те действия, которые ожидаются. Проверь условие задания.",
                Severity.Warning
            ));

        if (astMetrics.TryGetValue("ParameterSimilarity", out var paramSimilarity) && paramSimilarity < 0.8)
        {
            var mismatchCount = astMetrics.TryGetValue("ParameterMismatchCount", out var count) ? (int)count : 0;
            issues.Add(new CodeIssue(
                IssueType.ParameterMismatch,
                $"Параметры действий не соответствуют ожидаемым ({mismatchCount} несовпадений). Проверь числа и значения.",
                Severity.Warning
            ));
        }

        return issues;
    }

    private string GenerateHint(
        StateCheckResult stateResult,
        double traceSimilarity,
        Dictionary<string, double> astMetrics,
        bool isPassed,
        bool isOptimal)
    {
        if (isPassed && isOptimal)
            return "Отлично! Задание выполнено правильно и оптимально! 🎉";

        if (isPassed && !isOptimal)
        {
            if (astMetrics.GetValueOrDefault("RedundantCount", 0) > 0)
                return "Задание пройдено, но есть лишние действия. Попробуй сделать решение короче! 🧹";

            return "Задание пройдено, но порядок действий отличается от ожидаемого. Проверь последовательность шагов! 📋";
        }

        if (!stateResult.IsPassed)
        {
            if (stateResult.ErrorMessage != null && stateResult.ErrorMessage.Contains("Текст сказан не в том месте"))
                return "Кот сказал правильный текст, но не в том месте! Сначала нужно дойти до правильной позиции. 🎯";

            return "Что-то не так! Проверь, чтобы персонаж оказался в нужном месте. 🎯";
        }

        return "Попробуй ещё раз! Обрати внимание на последовательность действий. 💡";
    }
}

internal record StateCheckResult(bool IsPassed, string? ErrorMessage);