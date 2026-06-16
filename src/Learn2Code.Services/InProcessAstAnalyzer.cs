using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Learn2Code.Core;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Microsoft.Extensions.Logging;

namespace Learn2Code.Services;

public class InProcessAstAnalyzer : ILanguageAnalyzer
{
    private readonly ILogger<InProcessAstAnalyzer> _logger;
    private readonly string _pythonPath;
    private readonly string _sandboxDir;

    public InProcessAstAnalyzer(
        string pythonPath,
        string sandboxDir,
        ILogger<InProcessAstAnalyzer> logger)
    {
        _pythonPath = pythonPath;
        _sandboxDir = sandboxDir;
        _logger = logger;
    }

    public bool Supports(string languageId)
    {
        return languageId.ToLowerInvariant() is "python" or "python3";
    }

    public async Task<NormalizedProgram> ExtractAsync(string code)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"\"{Path.Combine(_sandboxDir, "ast_extractor.py")}\"",
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

        process.StandardInput.Write(code);
        process.StandardInput.Close();

        var exited = process.WaitForExit(10000);
        if (!exited)
        {
            process.Kill();
            throw new TimeoutException("Превышен лимит анализа AST (10 сек).");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        _logger.LogDebug("Python AST extractor stdout length: {Length}, stderr: {Stderr}", stdout.Length, stderr);

        if (string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogError("Python AST extractor returned empty output. stderr: {Stderr}", stderr);
            throw new InvalidOperationException($"AST extractor returned empty output. stderr: {stderr}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<AstExtractResult>(stdout, JsonOptions.Default);
            if (result == null || !result.Success)
            {
                var errMsg = result?.ToString() ?? "null result";
                _logger.LogError("AST extractor returned unsuccessful result. stdout: {Stdout}, stderr: {Stderr}",
                    stdout, stderr);
                throw new InvalidOperationException($"AST extractor error. stdout: {stdout}, stderr: {stderr}");
            }

            var program = new NormalizedProgram();
            foreach (var elem in result.Elements)
            {
                var parameters = new Dictionary<string, object?>();
                if (elem.Parameters != null)
                    foreach (var (key, value) in elem.Parameters)
                        if (value is JsonElement jsonElement)
                            parameters[key] = jsonElement.ValueKind switch
                            {
                                JsonValueKind.String => jsonElement.GetString()!,
                                JsonValueKind.Number => jsonElement.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => jsonElement.ToString()
                            };
                        else
                            parameters[key] = value;

                program.Elements.Add(new CodeElement
                {
                    Type = elem.Type ?? "Unknown",
                    SemanticHint = elem.SemanticHint ?? string.Empty,
                    Line = elem.Line > 0 ? elem.Line : null,
                    BlockId = elem.BlockId,
                    Parameters = parameters
                });
            }

            if (result.Metrics != null)
                foreach (var kv in result.Metrics)
                    program.Metrics[kv.Key] = kv.Value;

            program.Metrics["lineCount"] = code.Split('\n').Length;
            program.Metrics["elementCount"] = program.Elements.Count;

            return program;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ошибка десериализации AST. stdout: {Stdout}", stdout);
            throw new InvalidOperationException("Не удалось распарсить AST от Python-анализатора", ex);
        }
    }

    private record AstExtractResult(
        bool Success,
        List<AstElementDto> Elements,
        Dictionary<string, double>? Metrics
    );

    private record AstElementDto(
        string? Type,
        string? SemanticHint,
        int Line,
        string? BlockId,
        Dictionary<string, object>? Parameters
    );
}