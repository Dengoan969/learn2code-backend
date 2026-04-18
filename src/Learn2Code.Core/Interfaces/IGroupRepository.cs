using Learn2Code.Core.Entities;

namespace Learn2Code.Core.Interfaces;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id);
    Task<IEnumerable<Group>> GetAllAsync();
    Task<IEnumerable<Group>> GetByCourseIdAsync(Guid courseId);
    Task<IEnumerable<Group>> GetByTeacherIdAsync(Guid teacherId);
    Task<IEnumerable<Group>> GetByStudentIdAsync(Guid studentId);
    Task<Group> CreateAsync(Group group);
    Task UpdateAsync(Group group);
    Task DeleteAsync(Guid id);
    Task AddStudentToGroupAsync(Guid groupId, Guid studentId);
    Task RemoveStudentFromGroupAsync(Guid groupId, Guid studentId);
    Task<bool> IsStudentInGroupAsync(Guid groupId, Guid studentId);
    Task<IEnumerable<User>> GetStudentsInGroupAsync(Guid groupId);
}