using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Mappings;

public static class ExecutionTraceMapper
{
    public static ExecutionTraceDto ToDto(ExecutionTrace? model)
    {
        if (model == null) return null!;

        return new ExecutionTraceDto
        {
            Events = model.Events.Select(ExecutionEventMapper.ToDto).ToList()
        };
    }

    public static ExecutionTrace ToModel(ExecutionTraceDto? dto)
    {
        if (dto == null) return null!;

        return new ExecutionTrace
        {
            Events = dto.Events.Select(ExecutionEventMapper.ToModel).ToList()
        };
    }

    public static ExecutionResult ToModel(ExecutionResultDto dto)
    {
        if (dto == null) return null!;

        return new ExecutionResult
        {
            Success = dto.Success,
            Error = dto.Error,
            FinalState = SceneStateMapper.ToModel(dto.FinalState),
            Trace = ToModel(dto.Trace)
        };
    }

    public static ExecutionResultDto ToDto(ExecutionResult model)
    {
        if (model == null) return null!;

        return new ExecutionResultDto
        {
            Success = model.Success,
            Error = model.Error,
            FinalState = SceneStateMapper.ToDto(model.FinalState),
            Trace = ToDto(model.Trace)
        };
    }
}