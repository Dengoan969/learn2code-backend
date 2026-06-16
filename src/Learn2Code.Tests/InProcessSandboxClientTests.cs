using System.Diagnostics;
using Learn2Code.Core.Models;
using Learn2Code.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class InProcessSandboxClientTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<InProcessSandboxClient>>();

        _tempSandboxDir = Path.Combine(Path.GetTempPath(), $"sandbox_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempSandboxDir);

        var sandboxRunnerPath = Path.Combine(_tempSandboxDir, "sandbox_runner.py");
        File.WriteAllText(sandboxRunnerPath, """
                                             import sys
                                             import json

                                             def main():
                                                 input_data = sys.stdin.read()
                                                 try:
                                                     request = json.loads(input_data)
                                                 except json.JSONDecodeError:
                                                     print(json.dumps({"success": False, "error": "Invalid JSON"}))
                                                     sys.exit(1)
                                                 
                                                 code = request.get("code", "")
                                                 initial_state = request.get("initialState", {})
                                                 config = request.get("config", {})
                                                 
                                                 if "error" in code:
                                                     print(json.dumps({"success": False, "error": "Simulated error", "finalState": {}, "trace": []}))
                                                 elif "timeout" in code:
                                                     import time
                                                     time.sleep(35)
                                                 elif "empty" in code:
                                                     print(json.dumps({"success": True, "finalState": {"sprites": []}, "trace": {"events": []}}))
                                                 else:
                                                     result = {
                                                         "success": True,
                                                         "finalState": {
                                                             "sprites": [
                                                                 {
                                                                     "type": "Cat",
                                                                     "x": 10.0,
                                                                     "y": 5.0,
                                                                     "visible": True,
                                                                     "direction": 90.0,
                                                                     "costume": "default",
                                                                     "saidTexts": {},
                                                                     "collectedItems": {}
                                                                 }
                                                             ]
                                                         },
                                                         "trace": {
                                                             "events": [
                                                                 {"step": 1, "eventType": "move", "details": {"distance": 10}},
                                                                 {"step": 2, "eventType": "turn", "details": {"degrees": 90}}
                                                             ]
                                                         }
                                                     }
                                                     print(json.dumps(result))

                                             if __name__ == "__main__":
                                                 main()
                                             """);

        _pythonPath = FindPythonPath();
        if (_pythonPath == null) Assert.Inconclusive("Python not found on system. Skipping sandbox tests.");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempSandboxDir)) Directory.Delete(_tempSandboxDir, true);
        }
        catch
        {
        }
    }

    private Mock<ILogger<InProcessSandboxClient>> _mockLogger;
    private string _tempSandboxDir;
    private string _pythonPath;

    [Test]
    public async Task ExecuteAsync_WithValidCode_ReturnsSuccessPayload()
    {
        var client = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockLogger.Object);
        var code = "move(10)";
        var initialState = new SceneState();
        var config = new TaskConfig();

        var result = await client.ExecuteAsync(code, initialState, config);

        Console.WriteLine($"Result.Success: {result.Success}");
        Console.WriteLine($"Result.Error: {result.Error}");
        if (result.FinalState?.Sprites != null)
            Console.WriteLine($"Result.FinalState.Sprites.Count: {result.FinalState.Sprites.Count}");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.FinalState.Sprites, Has.Count.EqualTo(1));
        var catSprite = result.FinalState.Sprites[0] as CatState;
        Assert.That(catSprite, Is.Not.Null);
        Assert.That(catSprite.X, Is.EqualTo(10.0));
        Assert.That(catSprite.Y, Is.EqualTo(5.0));
        Assert.That(catSprite.Direction, Is.EqualTo(90.0));
        Assert.That(result.Trace.Events, Has.Count.EqualTo(2));
        Assert.That(result.Trace.Events[0].EventType, Is.EqualTo("move"));
    }

    [Test]
    public async Task ExecuteAsync_WithErrorInCode_ReturnsErrorPayload()
    {
        var client = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockLogger.Object);
        var code = "error simulation";
        var initialState = new SceneState();
        var config = new TaskConfig();

        var result = await client.ExecuteAsync(code, initialState, config);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
        Assert.That(result.FinalState.Sprites, Is.Empty);
        Assert.That(result.Trace.Events, Is.Empty);
    }

    [Test]
    public void ExecuteAsync_WithTimeout_ThrowsTimeoutException()
    {
        var client = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockLogger.Object);
        var code = "timeout simulation";
        var initialState = new SceneState();
        var config = new TaskConfig();

        Assert.ThrowsAsync<TimeoutException>(async () =>
            await client.ExecuteAsync(code, initialState, config));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyCode_ReturnsEmptyState()
    {
        var client = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockLogger.Object);
        var code = "empty simulation";
        var initialState = new SceneState();
        var config = new TaskConfig();

        var result = await client.ExecuteAsync(code, initialState, config);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.FinalState.Sprites, Is.Empty);
        Assert.That(result.Trace.Events, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidJsonResponse_ReturnsErrorResult()
    {
        var runnerPath = Path.Combine(_tempSandboxDir, "sandbox_runner.py");
        File.WriteAllText(runnerPath, """
                                      print("not json at all")
                                      """);

        var client = new InProcessSandboxClient(_pythonPath, _tempSandboxDir, _mockLogger.Object);
        var code = "any code";
        var initialState = new SceneState();
        var config = new TaskConfig();

        var result = await client.ExecuteAsync(code, initialState, config);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("Ошибка парсинга ответа"));
        Assert.That(result.FinalState.Sprites, Is.Empty);
        Assert.That(result.Trace.Events, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithPythonNotFound_ThrowsInvalidOperationException()
    {
        var client = new InProcessSandboxClient("nonexistent_python.exe", _tempSandboxDir, _mockLogger.Object);
        var code = "any code";
        var initialState = new SceneState();
        var config = new TaskConfig();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.ExecuteAsync(code, initialState, config));
    }

    [Test]
    public async Task ExecuteAsync_WithSandboxDirNotFound_ThrowsInvalidOperationException()
    {
        var client = new InProcessSandboxClient(_pythonPath, "nonexistent_directory", _mockLogger.Object);
        var code = "any code";
        var initialState = new SceneState();
        var config = new TaskConfig();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.ExecuteAsync(code, initialState, config));
    }

    private static string? FindPythonPath()
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
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0) return path;
                }
            }
            catch
            {
            }

        return null;
    }
}