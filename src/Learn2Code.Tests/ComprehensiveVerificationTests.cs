using System.Diagnostics;
using Learn2Code.Core.Enums;
using Learn2Code.Core.Models;
using Learn2Code.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class ComprehensiveVerificationTests
{
    [SetUp]
    public void SetUp()
    {
        _mockSandboxLogger = new Mock<ILogger<InProcessSandboxClient>>();
        _mockAnalyzerLogger = new Mock<ILogger<InProcessAstAnalyzer>>();

        Console.WriteLine($"SetUp: Current directory = {Directory.GetCurrentDirectory()}");

        _tempSandboxDir = Path.Combine(Path.GetTempPath(), $"sandbox_comprehensive_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempSandboxDir);
        Console.WriteLine($"SetUp: Temp sandbox dir = {_tempSandboxDir}");

        CopySandboxFiles();

        _pythonPath = FindPythonPath();
        if (_pythonPath == null)
        {
            Assert.Inconclusive("Python not found on system. Skipping comprehensive tests.");
            return;
        }

        Console.WriteLine($"SetUp: Python path = {_pythonPath}");

        _analyzer = new InProcessAstAnalyzer(_pythonPath, _tempSandboxDir, _mockAnalyzerLogger.Object);
        _sandboxClient = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockSandboxLogger.Object);
        _verificationEngine = new CoreComparisonEngine(_analyzer);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempSandboxDir))
                Directory.Delete(_tempSandboxDir, true);
        }
        catch { }
    }

    private Mock<ILogger<InProcessSandboxClient>> _mockSandboxLogger;
    private Mock<ILogger<InProcessAstAnalyzer>> _mockAnalyzerLogger;
    private string _tempSandboxDir;
    private string _pythonPath;
    private InProcessAstAnalyzer _analyzer;
    private InProcessSandboxClient _sandboxClient;
    private CoreComparisonEngine _verificationEngine;

    private void CopySandboxFiles()
    {

        var sourceDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "sandbox"));

        if (!Directory.Exists(sourceDir))
        {
            var workspaceDir =
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
            sourceDir = Path.Combine(workspaceDir, "src", "sandbox");
        }

        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"ERROR: Could not find sandbox directory. Tried: {sourceDir}");
            return;
        }

        Console.WriteLine($"Copying sandbox files from: {sourceDir} to {_tempSandboxDir}");

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(_tempSandboxDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
            Console.WriteLine($"  Copied: {Path.GetFileName(file)}");
        }

        var copiedFiles = Directory.GetFiles(_tempSandboxDir);
        Console.WriteLine($"Copied {copiedFiles.Length} files to temp directory");
    }

    private string FindPythonPath()
    {
        var possiblePaths = new[]
        {
            "python",
            "python3",
            "python.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python311",
                "python.exe")
        };

        foreach (var path in possiblePaths)
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(1000);
                if (process.ExitCode == 0)
                    return path;
            }
            catch { }

        return null;
    }

    [Test]
    public async Task FullPipeline_SimpleMove_ShouldPassAndBeOptimal()
    {
        var studentCode = """
                          move(10)
                          turn(90)
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 10.0,
                Y = 5.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.True, "Should be optimal (correct trace, no redundant code)");
        Assert.That(result.Metrics["StateScore"], Is.EqualTo(1.0));
        Assert.That(result.Metrics["TraceSimilarity"], Is.GreaterThanOrEqualTo(0.9));
        Assert.That(result.Metrics["AstSimilarity"], Is.GreaterThanOrEqualTo(0.9));
        Assert.That(result.Metrics["ParameterSimilarity"], Is.GreaterThanOrEqualTo(0.9));
        Assert.That(result.Metrics["RedundantCount"], Is.EqualTo(0));
        Assert.That(result.Metrics["MissingCount"], Is.EqualTo(0));
        Assert.That(result.Issues, Is.Empty, "Should have no issues for perfect solution");
    }

    [Test]
    public async Task FullPipeline_WrongParameters_ShouldPassButNotOptimal()
    {
        var studentCode = """
                          move(5)
                          turn(90)
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 5.0,
                Y = 5.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 180.0,
                Visible = true,
                Costume = "default",
                SaidTexts = new Dictionary<string, int>(),
                CollectedItems = new Dictionary<string, int>()
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        Console.WriteLine($"Execution success: {execution.Success}");
        if (!execution.Success)
        {
            Console.WriteLine($"Execution error: {execution.Error}");
        }
        else
        {
            Console.WriteLine($"Final state sprites count: {execution.FinalState?.Sprites?.Count ?? 0}");
            if (execution.FinalState?.Sprites?.Count > 0)
            {
                var cat = execution.FinalState.Sprites.OfType<CatState>().FirstOrDefault();
                if (cat != null)
                    Console.WriteLine(
                        $"Actual cat: X={cat.X}, Y={cat.Y}, Direction={cat.Direction}, Costume={cat.Costume}, Visible={cat.Visible}");
            }
        }

        var expectedCat = expectedState.Sprites.OfType<CatState>().FirstOrDefault();
        if (expectedCat != null)
            Console.WriteLine(
                $"Expected cat: X={expectedCat.X}, Y={expectedCat.Y}, Direction={expectedCat.Direction}, Costume={expectedCat.Costume}, Visible={expectedCat.Visible}");

        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check (tolerance allows difference)");
        Assert.That(result.IsOptimal, Is.False, "Should not be optimal due to parameter mismatch");
        Assert.That(result.Metrics["ParameterSimilarity"], Is.LessThan(1.0), "Parameter similarity should be < 1.0");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.ParameterMismatch), Is.True,
            "Should have ParameterMismatch issue");
    }

    [Test]
    public async Task FullPipeline_MissingAction_ShouldHaveMissingElementIssue()
    {
        var studentCode = """
                          move(10)
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 15.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check (reached expected position)");
        Assert.That(result.Metrics["MissingCount"], Is.GreaterThan(0), "Should have missing elements");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.MissingElement), Is.True,
            "Should have MissingElement issue");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.TraceMismatch), Is.True,
            "Should have TraceMismatch issue due to different sequence");
    }

    [Test]
    public async Task FullPipeline_ExtraActions_ShouldHaveRedundantCodeIssue()
    {
        var studentCode = """
                          move(10)
                          turn(90)
                          move(5)
                          turn(180)
                          move(0)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 10.0,
                Y = 5.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 0.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.False, "Should not be optimal due to redundant code");
        Assert.That(result.Metrics["RedundantCount"], Is.GreaterThan(0), "Should have redundant elements");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.RedundantCode), Is.True,
            "Should have RedundantCode issue");
    }

    [Test]
    public async Task FullPipeline_WrongOrder_ShouldHaveTraceMismatch()
    {
        var studentCode = """
                          turn(90)
                          move(10)
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 15.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check (reached expected position for this order)");
        Assert.That(result.Metrics["TraceSimilarity"], Is.LessThan(1.0), "Trace similarity should be < 1.0");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.TraceMismatch), Is.True,
            "Should have TraceMismatch issue");
    }

    [Test]
    public async Task FullPipeline_ComplexProgramWithLoops_ShouldAnalyzeCorrectly()
    {
        var studentCode = """
                          for i in range(3):
                              move(5)
                              turn(120)
                          """;

        var referenceCode = """
                            for i in range(3):
                                move(5)
                                turn(120)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 0,
                Y = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.True,
            "Identical code with loops should be optimal when compared to solution trace");
        Assert.That(result.Metrics["AstSimilarity"], Is.GreaterThanOrEqualTo(0.9));
    }

    [Test]
    public async Task FullPipeline_BlocklyGeneratedCode_ShouldWorkWithBlockIds()
    {
        var studentCode = """
                          # BLOCK_ID: move_block_1
                          move(10)
                          # BLOCK_ID: turn_block_2
                          turn(90)
                          # BLOCK_ID: move_block_3
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            turn(90)
                            move(5)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 10.0,
                Y = 5.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;

        Console.WriteLine($"DEBUG: execution.Success = {execution.Success}");
        Console.WriteLine($"DEBUG: execution.Error = {execution.Error}");
        Console.WriteLine($"DEBUG: initialState.Sprites.Count = {initialState.Sprites.Count}");
        Console.WriteLine($"DEBUG: execution.FinalState.Sprites.Count = {execution.FinalState.Sprites.Count}");
        var cat = execution.FinalState.Sprites.OfType<CatState>().FirstOrDefault();
        if (cat != null)
            Console.WriteLine($"DEBUG: cat at ({cat.X}, {cat.Y}), direction {cat.Direction}");
        else
            Console.WriteLine("DEBUG: cat not found in FinalState");
        Console.WriteLine($"DEBUG: execution.Trace.Events.Count = {execution.Trace?.Events?.Count}");
        foreach (var ev in execution.Trace?.Events ?? new List<ExecutionEvent>())
            Console.WriteLine($"DEBUG: event {ev.EventType} Details={ev.Details?.Count}");

        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.True, "Should be optimal");
        Assert.That(studentProgram.Elements.Any(e => e.BlockId != null), Is.True,
            "Should have elements with BlockId from BLOCK_ID comments");
    }

    [Test]
    public async Task FullPipeline_StateMismatch_ShouldFailWithError()
    {
        var studentCode = """
                          move(5)
                          """;

        var referenceCode = """
                            move(10)
                            """;

        var config = new TaskConfig
        {
            SceneWidth = 1000,
            SceneHeight = 1000,
            TolerancePx = 1.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        var initialState = new SceneState(
            new CatState
            {
                X = 0,
                Y = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var expectedState = new SceneState(
            new CatState
            {
                X = 10,
                Y = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            new ExecutionTrace());

        Assert.That(result.IsPassed, Is.False, "Should fail state check");
        Assert.That(result.Metrics["StateScore"], Is.EqualTo(0.0));
        Assert.That(result.Issues.Any(i => i.Type == IssueType.StateMismatch && i.Severity == Severity.Error), Is.True,
            "Should have StateMismatch error");
        Assert.That(result.Hint, Does.Contain("Что-то не так"), "Hint should indicate failure");
    }
}