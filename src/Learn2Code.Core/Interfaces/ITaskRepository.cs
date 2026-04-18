using Learn2Code.Core.Entities;

namespace Learn2Code.Core.Interfaces;

public interface ITaskRepository
{
    Task<EducationalTask?> GetByIdAsync(Guid id);
    Task<IEnumerable<EducationalTask>> GetByLessonIdAsync(Guid lessonId);
    Task<IEnumerable<EducationalTask>> GetAllAsync();
    Task<EducationalTask> CreateAsync(EducationalTask task);
    Task UpdateAsync(EducationalTask task);
    Task DeleteAsync(Guid id);
}