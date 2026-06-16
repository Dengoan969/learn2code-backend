using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Learn2Code.Core;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Microsoft.Extensions.Logging;

namespace Learn2Code.Services;

public class InProcessSandboxClient : ISandboxClient
{
    private readonly ILogger<InProcessSandboxClient> _logger;
    private readonly string _pythonPath;
    private readonly string _sandboxDir;

    public InProcessSandboxClient(
        string pythonPath,
        string sandboxDir,
        ILogger<InProcessSandboxClient> logger)
    {
        _pythonPath = pythonPath;
        _sandboxDir = sandboxDir;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(string code, SceneState initialState, TaskConfig config)
    {
        var request = new { code, initialState, config };
        var requestJson = JsonSerializer.Serialize(request, JsonOptions.Default);

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{Path.Combine(_sandboxDir, "sandbox_runner.py")}\"",
            WorkingDirectory = _sandboxDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Не удалось запустить Python: {_pythonPath}", ex);
        }

        if (process == null)
            throw new InvalidOperationException($"Не удалось запустить Python: {_pythonPath}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        process.StandardInput.WriteLine(requestJson);
        process.StandardInput.Close();

        var exited = process.WaitForExit(30000);
        if (!exited)
        {
            process.Kill();
            throw new TimeoutException("Превышен лимит выполнения (30 сек).");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        _logger.LogDebug("Python sandbox stdout: {Stdout}", stdout);
        _logger.LogDebug("Python sandbox stderr: {Stderr}", stderr);

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            return new ExecutionResult
            {
                Success = false,
                Error = $"Python exited with {process.ExitCode}: {stderr}",
                FinalState = new SceneState(),
                Trace = new ExecutionTrace()
            };

        try
        {
            _logger.LogDebug("Raw stdout from sandbox: {Stdout}", stdout);
            var resultDto = JsonSerializer.Deserialize<ExecutionResult>(stdout, JsonOptions.Default);
            if (resultDto == null)
                throw new InvalidOperationException("Пустой ответ от песочницы");

            return resultDto;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ошибка десериализации ответа песочницы. stdout: {Stdout}, stderr: {Stderr}", stdout,
                stderr);
            return new ExecutionResult
            {
                Success = false,
                Error = $"Ошибка парсинга ответа: {ex.Message}. stderr: {stderr}",
                FinalState = new SceneState(),
                Trace = new ExecutionTrace()
            };
        }
    }
}