using Learn2Code.Core.Entities;
using Learn2Code.Core.Models;

namespace Learn2Code.Core.Interfaces;

public interface IProgressRepository
{
    Task<Progress?> GetByStudentAndTaskAsync(Guid studentId, Guid taskId);
    Task<IEnumerable<Progress>> GetByStudentIdAsync(Guid studentId);
    Task SaveAsync(Guid studentId, Guid taskId, CheckResult result);
}