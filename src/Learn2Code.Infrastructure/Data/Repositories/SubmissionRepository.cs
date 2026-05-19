using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Infrastructure.Data.Repositories;

public class SubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _context;

    public SubmissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Submission?> GetByIdAsync(Guid id)
    {
        return await _context.Submissions.FindAsync(id);
    }

    public async Task<IEnumerable<Submission>> GetByStudentAndTaskAsync(Guid studentId, Guid taskId)
    {
        return await _context.Submissions
            .Where(s => s.StudentId == studentId && s.TaskId == taskId && !s.IsDraft)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Submission>> GetAllByTaskIdAsync(Guid taskId)
    {
        return await _context.Submissions
            .Where(s => s.TaskId == taskId && !s.IsDraft)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
    }

    public async Task<Submission> CreateAsync(Submission submission)
    {
        _context.Submissions.Add(submission);
        await _context.SaveChangesAsync();
        return submission;
    }

    public async Task UpdateAsync(Submission submission)
    {
        _context.Submissions.Update(submission);
        await _context.SaveChangesAsync();
    }

    public async Task<Submission?> GetDraftByTaskAndStudentAsync(Guid taskId, Guid studentId)
    {
        return await _context.Submissions
            .FirstOrDefaultAsync(s => s.TaskId == taskId &&
                                     s.StudentId == studentId &&
                                     s.IsDraft);
    }
}