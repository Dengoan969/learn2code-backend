using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Infrastructure.Data.Repositories;

public class ProgressRepository : IProgressRepository
{
    private readonly AppDbContext _context;

    public ProgressRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Progress?> GetByStudentAndTaskAsync(Guid studentId, Guid taskId)
    {
        return await _context.Progress
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.TaskId == taskId);
    }

    public async Task<IEnumerable<Progress>> GetByStudentIdAsync(Guid studentId)
    {
        return await _context.Progress
            .Where(p => p.StudentId == studentId)
            .ToListAsync();
    }

    public async Task SaveAsync(Guid studentId, Guid taskId, CheckResult result)
    {
        var progress = await GetByStudentAndTaskAsync(studentId, taskId);

        if (progress == null)
        {
            progress = new Progress
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                TaskId = taskId,
                Completed = result.IsPassed,
                AttemptsCount = 1,
                LastAttemptAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Progress.Add(progress);
        }
        else
        {
            progress.Completed = progress.Completed || result.IsPassed;
            progress.AttemptsCount++;
            progress.LastAttemptAt = DateTime.UtcNow;
            progress.UpdatedAt = DateTime.UtcNow;
            _context.Progress.Update(progress);
        }

        await _context.SaveChangesAsync();
    }
}