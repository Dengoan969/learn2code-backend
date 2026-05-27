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

        // Debug: log current directory
        Console.WriteLine($"SetUp: Current directory = {Directory.GetCurrentDirectory()}");

        // Create temporary sandbox directory
        _tempSandboxDir = Path.Combine(Path.GetTempPath(), $"sandbox_comprehensive_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempSandboxDir);
        Console.WriteLine($"SetUp: Temp sandbox dir = {_tempSandboxDir}");

        // Copy actual sandbox files for testing
        CopySandboxFiles();

        // Find Python executable
        _pythonPath = FindPythonPath();
        if (_pythonPath == null)
        {
            Assert.Inconclusive("Python not found on system. Skipping comprehensive tests.");
            return;
        }

        Console.WriteLine($"SetUp: Python path = {_pythonPath}");

        // Initialize real components
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
        catch
        {
            // Ignore cleanup errors
        }
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
        // Current directory is c:\Users\Dengo\Desktop\edu\src\Learn2Code.Tests\bin\Debug\net8.0
        // Sandbox is at c:\Users\Dengo\Desktop\edu\src\sandbox
        // Need to go up 3 levels: ..\..\..\sandbox

        var sourceDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "sandbox"));

        if (!Directory.Exists(sourceDir))
        {
            // Try alternative: from workspace root
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

        // Verify files were copied
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
            catch
            {
                // Continue to next path
            }

        return null;
    }

    [Test]
    public async Task FullPipeline_SimpleMove_ShouldPassAndBeOptimal()
    {
        // Arrange
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: after move(10) right to (10, 0), turn right 90° to face down (180°), move(5) down to (10, 5)
        // Note: move(10) = 10 pixels, move(5) = 5 pixels (1 unit = 1 pixel)
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
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
        // Arrange
        var studentCode = """
                          move(5)  # Should be 10
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: after move(5) right to (5, 0), turn right 90° to face down (180°), move(5) down to (5, 5)
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

        // Act
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
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check (tolerance allows difference)");
        Assert.That(result.IsOptimal, Is.False, "Should not be optimal due to parameter mismatch");
        Assert.That(result.Metrics["ParameterSimilarity"], Is.LessThan(1.0), "Parameter similarity should be < 1.0");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.ParameterMismatch), Is.True,
            "Should have ParameterMismatch issue");
    }

    [Test]
    public async Task FullPipeline_MissingAction_ShouldHaveMissingElementIssue()
    {
        // Arrange
        var studentCode = """
                          move(10)
                          # Missing turn
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: after move(10) right then move(5) right without turning = total 15 right
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
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
        // Arrange
        var studentCode = """
                          move(10)
                          turn(90)
                          move(5)
                          turn(180)  # Extra unnecessary turn
                          move(0)    # Extra zero move
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: after move(10) right to (10, 0), turn right 90° to face down (180°), move(5) down to (10, 5), then extra turn 180° to face up (0°)
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.False, "Should not be optimal due to redundant code");
        Assert.That(result.Metrics["RedundantCount"], Is.GreaterThan(0), "Should have redundant elements");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.RedundantCode), Is.True,
            "Should have RedundantCode issue");
    }

    [Test]
    public async Task FullPipeline_WrongOrder_ShouldHaveTraceMismatch()
    {
        // Arrange
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: turn right first (direction 180°), then move 10 down, then 5 down
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check (reached expected position for this order)");
        Assert.That(result.Metrics["TraceSimilarity"], Is.LessThan(1.0), "Trace similarity should be < 1.0");
        Assert.That(result.Issues.Any(i => i.Type == IssueType.TraceMismatch), Is.True,
            "Should have TraceMismatch issue");
    }

    [Test]
    public async Task FullPipeline_ComplexProgramWithLoops_ShouldAnalyzeCorrectly()
    {
        // Arrange
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

        // After 3 moves of 5 at 120-degree turns, we get a triangle
        // Final position is back near start (0, 0) with some floating point error
        var config = new TaskConfig
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0,
            TolerancePx = 1.0, // Larger tolerance due to floating point errors
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: back at start (0, 0) after triangle
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            solutionTrace);

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        // With solution trace comparison, identical code (including loops) should be optimal
        Assert.That(result.IsOptimal, Is.True,
            "Identical code with loops should be optimal when compared to solution trace");
        Assert.That(result.Metrics["AstSimilarity"], Is.GreaterThanOrEqualTo(0.9));
    }

    [Test]
    public async Task FullPipeline_BlocklyGeneratedCode_ShouldWorkWithBlockIds()
    {
        // Arrange - Simulate Blockly-generated Python code with BLOCK_ID comments
        var studentCode = """
                          # BLOCK_ID: move_block_1
                          move(10)  # Move forward 10 pixels
                          # BLOCK_ID: turn_block_2
                          turn(90)  # Turn right 90 degrees
                          # BLOCK_ID: move_block_3
                          move(5)   # Move forward 5 pixels
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: after move(10) right to (10, 0), turn right 90° to face down (180°), move(5) down to (10, 5)
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        // Get solution trace by executing reference code
        var solutionExecution = await _sandboxClient.ExecuteAsync(referenceCode, initialState, config);
        var solutionTrace = solutionExecution.Trace;

        // Debug output
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

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        Assert.That(result.IsOptimal, Is.True, "Should be optimal");
        // Check that block IDs are preserved in AST elements
        Assert.That(studentProgram.Elements.Any(e => e.BlockId != null), Is.True,
            "Should have elements with BlockId from BLOCK_ID comments");
    }

    [Test]
    public async Task FullPipeline_StateMismatch_ShouldFailWithError()
    {
        // Arrange
        var studentCode = """
                          move(5)  # Only move 5 instead of 10
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

        // Initial state: cat at (0, 0) facing right (90 degrees)
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

        // Expected state: Expect cat at (10, 0)
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

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config,
            new ExecutionTrace());

        // Assert
        Assert.That(result.IsPassed, Is.False, "Should fail state check");
        Assert.That(result.Metrics["StateScore"], Is.EqualTo(0.0));
        Assert.That(result.Issues.Any(i => i.Type == IssueType.StateMismatch && i.Severity == Severity.Error), Is.True,
            "Should have StateMismatch error");
        Assert.That(result.Hint, Does.Contain("Что-то не так"), "Hint should indicate failure");
    }
}