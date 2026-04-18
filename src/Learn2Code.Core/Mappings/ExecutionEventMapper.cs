using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Mappings;

public static class ExecutionEventMapper
{
    public static ExecutionEventDto ToDto(ExecutionEvent model)
    {
        if (model == null) return null!;
        
        return new ExecutionEventDto
        {
            Step = model.Step,
            EventType = model.EventType,
            Details = new Dictionary<string, object>(model.Details)
        };
    }

    public static ExecutionEvent ToModel(ExecutionEventDto dto)
    {
        if (dto == null) return null!;
        
        return new ExecutionEvent
        {
            Step = dto.Step,
            EventType = dto.EventType,
            Details = new Dictionary<string, object>(dto.Details)
        };
    }
}