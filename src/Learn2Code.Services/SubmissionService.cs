using System.Text.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;

namespace Learn2Code.Services;

public class SubmissionService
{
    private readonly IVerificationEngine _engine;
    private readonly IProgressRepository _progress;
    private readonly ISandboxClient _sandbox;
    private readonly ISubmissionRepository _submissions;
    private readonly ITaskRepository _tasks;

    public SubmissionService(
        ITaskRepository tasks,
        ISandboxClient sandbox,
        IVerificationEngine engine,
        IProgressRepository progress,
        ISubmissionRepository submissions)
    {
        _tasks = tasks;
        _sandbox = sandbox;
        _engine = engine;
        _progress = progress;
        _submissions = submissions;
    }

    public async Task<CheckResult> CheckAsync(Guid taskId, SubmissionRequest req)
    {
        // 1. Загрузка задания
        var task = await _tasks.GetByIdAsync(taskId);
        if (task == null)
            throw new KeyNotFoundException($"Task with id {taskId} not found");

        // 2. Исполнение в песочнице (используем типизированные свойства)
        var execResult = await _sandbox.ExecuteAsync(req.Code, task.InitialState, task.Config);

        // 3. Нормализация AST
        var analyzer = _engine.Analyzer;
        var studentProgram = await analyzer.ExtractAsync(req.Code);
        
        // Handle null solution code - create empty reference program
        var referenceProgram = task.SolutionCode != null
            ? await analyzer.ExtractAsync(task.SolutionCode)
            : new NormalizedProgram { Elements = new List<CodeElement>() };

        // 4. Сравнение - provide default SceneState if null
        var expectedState = task.ExpectedFinalState ?? new SceneState();
        var verdict = _engine.Compare(studentProgram, referenceProgram, execResult, expectedState, task.Config);

        // 5. Сохранение результата
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = Guid.Parse(req.StudentId),
            TaskId = taskId,
            Code = req.Code,
            BlocklyXml = req.BlocklyXml ?? string.Empty,
            BlockMapJson = JsonSerializer.Serialize(req.BlockMap),
            Language = req.Language,
            IsPassed = verdict.IsPassed,
            IsOptimal = verdict.IsOptimal,
            ResultJson = JsonSerializer.Serialize(verdict),
            SubmittedAt = DateTime.UtcNow
        };
        await _submissions.CreateAsync(submission);
        await _progress.SaveAsync(Guid.Parse(req.StudentId), taskId, verdict);

        return verdict;
    }
}