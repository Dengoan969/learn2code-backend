using Learn2Code.Core.Entities;

namespace Learn2Code.Core.Interfaces;

public interface ISubmissionRepository
{
    Task<Submission?> GetByIdAsync(Guid id);
    Task<IEnumerable<Submission>> GetByStudentAndTaskAsync(Guid studentId, Guid taskId);
    Task<IEnumerable<Submission>> GetAllByTaskIdAsync(Guid taskId);
    Task<Submission> CreateAsync(Submission submission);
}