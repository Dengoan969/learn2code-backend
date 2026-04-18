using Learn2Code.Core.Models;

namespace Learn2Code.Core.Interfaces;

public interface ISandboxClient
{
    Task<ExecutionResult> ExecuteAsync(string code, SceneState initialState, TaskConfig config);
}