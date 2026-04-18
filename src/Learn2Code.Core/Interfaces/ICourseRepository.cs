using Learn2Code.Core.Entities;

namespace Learn2Code.Core.Interfaces;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id);
    Task<IEnumerable<Course>> GetAllAsync();
    Task<IEnumerable<Course>> GetByTeacherIdAsync(Guid teacherId);
    Task<IEnumerable<Course>> GetByStudentIdAsync(Guid studentId);
    Task<Course> CreateAsync(Course course);
    Task UpdateAsync(Course course);
    Task DeleteAsync(Guid id);
}