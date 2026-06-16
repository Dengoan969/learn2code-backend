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
            SceneWidth = model.SceneWidth,
            SceneHeight = model.SceneHeight
        };
    }

    public static TaskConfig ToModel(TaskConfigDto dto)
    {
        if (dto == null) return null!;

        return new TaskConfig
        {
            SceneWidth = dto.SceneWidth,
            SceneHeight = dto.SceneHeight
        };
    }
}