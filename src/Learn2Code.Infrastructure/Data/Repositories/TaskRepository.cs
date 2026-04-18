using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Infrastructure.Data.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EducationalTask?> GetByIdAsync(Guid id)
    {
        return await _context.Tasks
            .Include(t => t.Lesson)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<EducationalTask>> GetByLessonIdAsync(Guid lessonId)
    {
        return await _context.Tasks
            .Include(t => t.Lesson)
            .Where(t => t.LessonId == lessonId)
            .OrderBy(t => t.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<EducationalTask>> GetAllAsync()
    {
        return await _context.Tasks
            .Include(t => t.Lesson)
            .OrderBy(t => t.LessonId)
            .ThenBy(t => t.Order)
            .ToListAsync();
    }

    public async Task<EducationalTask> CreateAsync(EducationalTask task)
    {
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task UpdateAsync(EducationalTask task)
    {
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task != null)
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}