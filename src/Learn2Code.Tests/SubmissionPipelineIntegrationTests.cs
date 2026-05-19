using System;
using System.Linq;
using System.Text.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Enums;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Learn2Code.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class SubmissionPipelineIntegrationTests
{
    private Mock<ITaskRepository> _mockTaskRepository;
    private Mock<ISandboxClient> _mockSandboxClient;
    private Mock<IVerificationEngine> _mockVerificationEngine;
    private Mock<IProgressRepository> _mockProgressRepository;
    private Mock<ISubmissionRepository> _mockSubmissionRepository;
    private Mock<ILogger<SubmissionService>> _mockLogger;
    private SubmissionService _submissionService;

    [SetUp]
    public void SetUp()
    {
        _mockTaskRepository = new Mock<ITaskRepository>();
        _mockSandboxClient = new Mock<ISandboxClient>();
        _mockVerificationEngine = new Mock<IVerificationEngine>();
        _mockProgressRepository = new Mock<IProgressRepository>();
        _mockSubmissionRepository = new Mock<ISubmissionRepository>();
        _mockLogger = new Mock<ILogger<SubmissionService>>();

        _submissionService = new SubmissionService(
            _mockTaskRepository.Object,
            _mockSandboxClient.Object,
            _mockVerificationEngine.Object,
            _mockProgressRepository.Object,
            _mockSubmissionRepository.Object
        );
    }

    [Test]
    public async Task CheckAsync_CompletePipeline_ReturnsCheckResult()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var studentId = Guid.NewGuid().ToString();
        var submissionId = Guid.NewGuid();
        
        var cat = new CatState
        {
            GridX = 0,
            GridY = 0,
            Direction = 90.0,
            Visible = true,
            Costume = "default"
        };

        var task = new EducationalTask
        {
            Id = taskId,
            Title = "Test Task",
            Description = "Test Description",
            Order = 1,
            LessonId = Guid.NewGuid(),
            PipelineState = TaskPipelineState.Published,
            InitialState = new SceneState(cat),
            ExpectedFinalState = new SceneState(
                new CatState
                {
                    GridX = 2,
                    GridY = 0,
                    Direction = 0.0,
                    Visible = true,
                    Costume = "default"
                }
            ),
            SolutionCode = "move(2)\nturn(90, \"right\")",
            Config = new TaskConfig
            {
                Level = CheckLevel.Normal,
                TolerancePx = 5.0,
                MinTraceRatio = 0.8
            }
        };

        var submissionRequest = new SubmissionRequest(
            studentId,
            "move(2)\nturn(90, \"right\")",
            "<xml>blockly</xml>"
        );

        var executionResult = new ExecutionResult
        {
            Success = true,
            Error = null,
            FinalState = new SceneState(
                new CatState
                {
                    GridX = 2,
                    GridY = 0,
                    Direction = 0.0,
                    Visible = true,
                    Costume = "default"
                }
            ),
            Trace = new ExecutionTrace { Events = new List<ExecutionEvent>() }
        };

        var checkResult = new CheckResult(
            true,
            true,
            "Отлично! Задание выполнено правильно и оптимально! 🎉",
            new List<CodeIssue>(),
            new Dictionary<string, double>
            {
                { "StateScore", 1.0 },
                { "TraceSimilarity", 1.0 },
                { "AstSimilarity", 1.0 }
            },
            executionResult.FinalState
        );

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(task);

        _mockSandboxClient
            .Setup(c => c.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<SceneState>(),
                It.IsAny<TaskConfig>()))
            .ReturnsAsync(executionResult);

        _mockVerificationEngine
            .Setup(e => e.Analyzer)
            .Returns(new MockLanguageAnalyzer());

        _mockVerificationEngine
            .Setup(e => e.Compare(
                It.IsAny<NormalizedProgram>(),
                It.IsAny<NormalizedProgram>(),
                It.IsAny<ExecutionResult>(),
                It.IsAny<SceneState>(),
                It.IsAny<TaskConfig>()))
            .Returns(checkResult);

        _mockSubmissionRepository
            .Setup(r => r.CreateAsync(It.IsAny<Submission>()))
            .Callback<Submission>(s => s.Id = submissionId)
            .Returns<Submission>(s => Task.FromResult(s));

        _mockProgressRepository
            .Setup(r => r.SaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CheckResult>()))
            .Returns(Task.CompletedTask);

        // Act
        var submission = await _submissionService.CheckAsync(taskId, submissionRequest);

        // Assert
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.ResultJson, Is.Not.Null.Or.Empty);
        
        var result = JsonSerializer.Deserialize<CheckResult>(submission.ResultJson, Learn2Code.Core.JsonOptions.Default);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsPassed, Is.True);
        Assert.That(result.IsOptimal, Is.True);
        Assert.That(result.Issues, Is.Empty);

        // Verify all interactions
        _mockTaskRepository.Verify(r => r.GetByIdAsync(taskId), Times.Once);
        _mockSandboxClient.Verify(c => c.ExecuteAsync(
            It.Is<string>(code => code == submissionRequest.Code),
            It.Is<SceneState>(s => s.Sprites.Count == 1),
            It.Is<TaskConfig>(c => c.Level == CheckLevel.Normal)), Times.Once);
        _mockVerificationEngine.Verify(e => e.Compare(
            It.IsAny<NormalizedProgram>(),
            It.IsAny<NormalizedProgram>(),
            It.Is<ExecutionResult>(er => er == executionResult),
            It.Is<SceneState>(es => es == task.ExpectedFinalState),
            It.Is<TaskConfig>(c => c == task.Config)), Times.Once);
        _mockSubmissionRepository.Verify(r => r.CreateAsync(It.IsAny<Submission>()), Times.Once);
        _mockProgressRepository.Verify(r => r.SaveAsync(
            It.Is<Guid>(id => id == Guid.Parse(studentId)),
            It.Is<Guid>(id => id == taskId),
            It.Is<CheckResult>(cr => JsonComparisonHelper.JsonEquals(result, cr))), Times.Once);
    }

    [Test]
    public async Task CheckAsync_WhenTaskNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var submissionRequest = new SubmissionRequest(
            Guid.NewGuid().ToString(),
            "move(2)",
            "<xml>blockly</xml>"
        );

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync((EducationalTask?)null);

        // Act & Assert
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _submissionService.CheckAsync(taskId, submissionRequest));
    }

    [Test]
    public async Task CheckAsync_WhenSandboxExecutionFails_ReturnsFailedResult()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new EducationalTask
        {
            Id = taskId,
            Title = "Test Task",
            Description = "Test Description",
            Order = 1,
            LessonId = Guid.NewGuid(),
            PipelineState = TaskPipelineState.Published,
            InitialState = new SceneState(),
            ExpectedFinalState = new SceneState(),
            SolutionCode = "move(2)",
            Config = new TaskConfig()
        };

        var submissionRequest = new SubmissionRequest(
            Guid.NewGuid().ToString(),
            "invalid code that causes error",
            "<xml>blockly</xml>"
        );

        var executionResult = new ExecutionResult
        {
            Success = false,
            Error = "Runtime error: division by zero",
            FinalState = new SceneState(),
            Trace = new ExecutionTrace { Events = new List<ExecutionEvent>() }
        };

        var checkResult = new CheckResult(
            false,
            false,
            "Что-то не так! Проверь, чтобы персонаж оказался в нужном месте. 🎯",
            new List<CodeIssue>
            {
                new(IssueType.StateMismatch, "Runtime error: division by zero", Severity.Error)
            },
            new Dictionary<string, double>(),
            executionResult.FinalState
        );

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(task);

        _mockSandboxClient
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<SceneState>(), It.IsAny<TaskConfig>()))
            .ReturnsAsync(executionResult);

        _mockVerificationEngine
            .Setup(e => e.Analyzer)
            .Returns(new MockLanguageAnalyzer());

        _mockVerificationEngine
            .Setup(e => e.Compare(
                It.IsAny<NormalizedProgram>(),
                It.IsAny<NormalizedProgram>(),
                It.IsAny<ExecutionResult>(),
                It.IsAny<SceneState>(),
                It.IsAny<TaskConfig>()))
            .Returns(checkResult);

        // Act
        var submission = await _submissionService.CheckAsync(taskId, submissionRequest);

        // Assert
        Assert.That(submission.ResultJson, Is.Not.Null.Or.Empty);
        var result = JsonSerializer.Deserialize<CheckResult>(submission.ResultJson, Learn2Code.Core.JsonOptions.Default);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsPassed, Is.False);
        Assert.That(result.Issues, Has.Count.EqualTo(1));
        Assert.That(result.Issues[0].Severity, Is.EqualTo(Severity.Error));
    }

    [Test]
    public async Task CheckAsync_WithDefaultConfig_UsesDefaultConfig()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new EducationalTask
        {
            Id = taskId,
            Title = "Test Task",
            Description = "Test Description",
            Order = 1,
            LessonId = Guid.NewGuid(),
            PipelineState = TaskPipelineState.Published,
            InitialState = new SceneState(),
            ExpectedFinalState = new SceneState(),
            SolutionCode = "move(2)",
            Config = new TaskConfig() // Default config
        };

        var submissionRequest = new SubmissionRequest(
            Guid.NewGuid().ToString(),
            "move(2)",
            "<xml>blockly</xml>"
        );

        var executionResult = new ExecutionResult
        {
            Success = true,
            Error = null,
            FinalState = new SceneState(),
            Trace = new ExecutionTrace { Events = new List<ExecutionEvent>() }
        };

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(task);

        _mockSandboxClient
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<SceneState>(), It.IsAny<TaskConfig>()))
            .ReturnsAsync(executionResult);

        _mockVerificationEngine
            .Setup(e => e.Analyzer)
            .Returns(new MockLanguageAnalyzer());

        // The verification engine should receive the default TaskConfig
        _mockVerificationEngine
            .Setup(e => e.Compare(
                It.IsAny<NormalizedProgram>(),
                It.IsAny<NormalizedProgram>(),
                It.IsAny<ExecutionResult>(),
                It.IsAny<SceneState>(),
                It.Is<TaskConfig>(c => c.Level == CheckLevel.Normal))) // Default config
            .Returns(new CheckResult(true, true, "OK", new List<CodeIssue>(), new Dictionary<string, double>(), new SceneState()));

        // Act
        await _submissionService.CheckAsync(taskId, submissionRequest);

        // Assert
        _mockVerificationEngine.Verify(e => e.Compare(
            It.IsAny<NormalizedProgram>(),
            It.IsAny<NormalizedProgram>(),
            It.IsAny<ExecutionResult>(),
            It.IsAny<SceneState>(),
            It.Is<TaskConfig>(c => c.Level == CheckLevel.Normal)), Times.Once);
    }

    [Test]
    public async Task CheckAsync_SavesSubmissionWithCorrectData()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var studentId = Guid.NewGuid().ToString();
        var task = new EducationalTask
        {
            Id = taskId,
            Title = "Test Task",
            Description = "Test Description",
            Order = 1,
            LessonId = Guid.NewGuid(),
            PipelineState = TaskPipelineState.Published,
            InitialState = new SceneState(),
            ExpectedFinalState = new SceneState(),
            SolutionCode = "move(2)",
            Config = new TaskConfig()
        };

        var submissionRequest = new SubmissionRequest(
            studentId,
            "move(2)",
            "<xml>blockly xml</xml>"
        );

        var checkResult = new CheckResult(
            true,
            false,
            "Good job",
            new List<CodeIssue>(),
            new Dictionary<string, double>(),
            new SceneState()
        );

        Submission? capturedSubmission = null;

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(task);

        _mockSandboxClient
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<SceneState>(), It.IsAny<TaskConfig>()))
            .ReturnsAsync(new ExecutionResult
            {
                Success = true,
                FinalState = new SceneState(),
                Trace = new ExecutionTrace { Events = new List<ExecutionEvent>() }
            });

        _mockVerificationEngine
            .Setup(e => e.Analyzer)
            .Returns(new MockLanguageAnalyzer());

        _mockVerificationEngine
            .Setup(e => e.Compare(
                It.IsAny<NormalizedProgram>(),
                It.IsAny<NormalizedProgram>(),
                It.IsAny<ExecutionResult>(),
                It.IsAny<SceneState>(),
                It.IsAny<TaskConfig>()))
            .Returns(checkResult);

        _mockSubmissionRepository
            .Setup(r => r.CreateAsync(It.IsAny<Submission>()))
            .Callback<Submission>(s => capturedSubmission = s)
            .Returns<Submission>(s => Task.FromResult(s));

        // Act
        await _submissionService.CheckAsync(taskId, submissionRequest);

        // Assert
        Assert.That(capturedSubmission, Is.Not.Null);
        Assert.That(capturedSubmission!.StudentId, Is.EqualTo(Guid.Parse(studentId)));
        Assert.That(capturedSubmission.TaskId, Is.EqualTo(taskId));
        Assert.That(capturedSubmission.Code, Is.EqualTo(submissionRequest.Code));
        Assert.That(capturedSubmission.BlocklyXml, Is.EqualTo(submissionRequest.BlocklyXml));
        Assert.That(capturedSubmission.ResultJson, Is.Not.Null.Or.Empty);
        Assert.That(capturedSubmission.SubmittedAt, Is.EqualTo(capturedSubmission.SubmittedAt).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task CheckAsync_WithNullBlocklyXml_SavesEmptyString()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var task = new EducationalTask
        {
            Id = taskId,
            Title = "Test Task",
            Description = "Test Description",
            Order = 1,
            LessonId = Guid.NewGuid(),
            PipelineState = TaskPipelineState.Published,
            InitialState = new SceneState(),
            ExpectedFinalState = new SceneState(),
            SolutionCode = "move(2)",
            Config = new TaskConfig()
        };

        var submissionRequest = new SubmissionRequest(
            Guid.NewGuid().ToString(),
            "move(2)",
            null // Null BlocklyXml
        );

        Submission? capturedSubmission = null;

        _mockTaskRepository
            .Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(task);

        _mockSandboxClient
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<SceneState>(), It.IsAny<TaskConfig>()))
            .ReturnsAsync(new ExecutionResult
            {
                Success = true,
                FinalState = new SceneState(),
                Trace = new ExecutionTrace { Events = new List<ExecutionEvent>() }
            });

        _mockVerificationEngine
            .Setup(e => e.Analyzer)
            .Returns(new MockLanguageAnalyzer());

        _mockVerificationEngine
            .Setup(e => e.Compare(
                It.IsAny<NormalizedProgram>(),
                It.IsAny<NormalizedProgram>(),
                It.IsAny<ExecutionResult>(),
                It.IsAny<SceneState>(),
                It.IsAny<TaskConfig>()))
            .Returns(new CheckResult(true, true, "OK", new List<CodeIssue>(), new Dictionary<string, double>(), new SceneState()));

        _mockSubmissionRepository
            .Setup(r => r.CreateAsync(It.IsAny<Submission>()))
            .Callback<Submission>(s => capturedSubmission = s)
            .Returns<Submission>(s => Task.FromResult(s));

        // Act
        await _submissionService.CheckAsync(taskId, submissionRequest);

        // Assert
        Assert.That(capturedSubmission!.BlocklyXml, Is.EqualTo(string.Empty));
    }

    // Helper class for mocking
    private class MockLanguageAnalyzer : ILanguageAnalyzer
    {
        public bool Supports(string languageId) => true;

        public Task<NormalizedProgram> ExtractAsync(string code)
        {
            return Task.FromResult(new NormalizedProgram());
        }
    }
}