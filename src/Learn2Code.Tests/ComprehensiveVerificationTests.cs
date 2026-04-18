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
    private Mock<ILogger<InProcessSandboxClient>> _mockSandboxLogger;
    private Mock<ILogger<InProcessAstAnalyzer>> _mockAnalyzerLogger;
    private string _tempSandboxDir;
    private string _pythonPath;
    private InProcessAstAnalyzer _analyzer;
    private InProcessSandboxClient _sandboxClient;
    private CoreComparisonEngine _verificationEngine;

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

    private void CopySandboxFiles()
    {
        // Current directory is c:\Users\Dengo\Desktop\edu\src\Learn2Code.Tests\bin\Debug\net8.0
        // Sandbox is at c:\Users\Dengo\Desktop\edu\src\sandbox
        // Need to go up 3 levels: ..\..\..\sandbox
        
        var sourceDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "sandbox"));
        
        if (!Directory.Exists(sourceDir))
        {
            // Try alternative: from workspace root
            var workspaceDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python311", "python.exe")
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
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
        }
        return null;
    }

    [Test]
    public async Task FullPipeline_SimpleMove_ShouldPassAndBeOptimal()
    {
        // Arrange
        var studentCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: after move(2) right to (2, 0), turn right 90° to face down (180°), move(1) down to (2, 1)
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 2,
                GridY = 1,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
            move(1)  # Should be 2
            turn(90, "right")
            move(1)
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: after move(1) right to (1, 0), turn right 90° to face down (180°), move(1) down to (1, 1)
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 1,
                GridY = 1,
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
                {
                    Console.WriteLine($"Actual cat: GridX={cat.GridX}, GridY={cat.GridY}, Direction={cat.Direction}, Costume={cat.Costume}, Visible={cat.Visible}");
                }
            }
        }
        
        var expectedCat = expectedState.Sprites.OfType<CatState>().FirstOrDefault();
        if (expectedCat != null)
        {
            Console.WriteLine($"Expected cat: GridX={expectedCat.GridX}, GridY={expectedCat.GridY}, Direction={expectedCat.Direction}, Costume={expectedCat.Costume}, Visible={expectedCat.Visible}");
        }
        
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
            move(2)
            # Missing turn
            move(1)
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: after move(2) right then move(1) right without turning = total 3 right
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 3,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
            move(2)
            turn(90, "right")
            move(1)
            turn(180, "left")  # Extra unnecessary turn
            move(0)            # Extra zero move
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: after move(2) right to (2, 0), turn right 90° to face down (180°), move(1) down to (2, 1), then extra turn left 180° to face up (0°)
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 2,
                GridY = 1,
                Direction = 0.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
            turn(90, "right")
            move(2)
            move(1)
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: turn right first (direction 180°), then move 2 down, then 1 down
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 3,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
                move(1)
                turn(120, "right")
            """;
        
        var referenceCode = """
            for i in range(3):
                move(1)
                turn(120, "right")
            """;
        
        // After 3 moves of 1 at 120-degree turns, we get a triangle
        // Final position is back near start (0, 0) with some floating point error
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 10.0,  // Larger tolerance due to floating point errors
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: back at start (0, 0) after triangle
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.True, "Should pass state check");
        // Loops generate many low-level trace events, making trace similarity low
        // Therefore the solution is not optimal despite identical AST
        Assert.That(result.IsOptimal, Is.False, "Loops produce low trace similarity, so not optimal");
        Assert.That(result.Metrics["AstSimilarity"], Is.GreaterThanOrEqualTo(0.9));
    }

    [Test]
    public async Task FullPipeline_BlocklyGeneratedCode_ShouldWorkWithBlockIds()
    {
        // Arrange - Simulate Blockly-generated Python code with BLOCK_ID comments
        var studentCode = """
            # BLOCK_ID: move_block_1
            move(2)  # Move forward 2 steps
            # BLOCK_ID: turn_block_2
            turn(90, "right")  # Turn right 90 degrees
            # BLOCK_ID: move_block_3
            move(1)  # Move forward 1 step
            """;
        
        var referenceCode = """
            move(2)
            turn(90, "right")
            move(1)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: after move(2) right to (2, 0), turn right 90° to face down (180°), move(1) down to (2, 1)
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 2,
                GridY = 1,
                Direction = 180.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

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
            move(1)  # Only move 1 instead of 2
            """;
        
        var referenceCode = """
            move(2)
            """;
        
        var config = new TaskConfig
        {
            GridWidth = 20,
            GridHeight = 20,
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = CheckLevel.Normal
        };
        
        // Initial state: cat at (0, 0) facing right (90 degrees)
        var initialState = new SceneState(
            new CatState
            {
                GridX = 0,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );
        
        // Expected state: Expect cat at (2, 0)
        var expectedState = new SceneState(
            new CatState
            {
                GridX = 2,
                GridY = 0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        // Act
        var execution = await _sandboxClient.ExecuteAsync(studentCode, initialState, config);
        var studentProgram = await _analyzer.ExtractAsync(studentCode);
        var referenceProgram = await _analyzer.ExtractAsync(referenceCode);
        var result = _verificationEngine.Compare(studentProgram, referenceProgram, execution, expectedState, config);

        // Assert
        Assert.That(result.IsPassed, Is.False, "Should fail state check");
        Assert.That(result.Metrics["StateScore"], Is.EqualTo(0.0));
        Assert.That(result.Issues.Any(i => i.Type == IssueType.StateMismatch && i.Severity == Severity.Error), Is.True,
            "Should have StateMismatch error");
        Assert.That(result.Hint, Does.Contain("Что-то не так"), "Hint should indicate failure");
    }
}