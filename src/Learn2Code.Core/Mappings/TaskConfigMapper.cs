using Learn2Code.Core.DTOs;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Mappings;

public static class TaskConfigMapper
{
    public static TaskConfigDto ToDto(TaskConfig model)
    {
        if (model == null) return null!;

        return new TaskConfigDto
        {
            GridWidth = model.GridWidth,
            GridHeight = model.GridHeight
        };
    }

    public static TaskConfig ToModel(TaskConfigDto dto)
    {
        if (dto == null) return null!;

        return new TaskConfig
        {
            GridWidth = dto.GridWidth,
            GridHeight = dto.GridHeight,
            // Set default values for other properties that are not in DTO
            TolerancePx = 5.0,
            MinTraceRatio = 0.7,
            Level = Enums.CheckLevel.Normal
        };
    }

    // Overload for when we need to preserve other properties from existing model
    public static TaskConfig ToModel(TaskConfigDto dto, TaskConfig existing)
    {
        if (dto == null) return null!;

        return new TaskConfig
        {
            GridWidth = dto.GridWidth,
            GridHeight = dto.GridHeight,
            TolerancePx = existing?.TolerancePx ?? 5.0,
            MinTraceRatio = existing?.MinTraceRatio ?? 0.7,
            Level = existing?.Level ?? Enums.CheckLevel.Normal
        };
    }
}