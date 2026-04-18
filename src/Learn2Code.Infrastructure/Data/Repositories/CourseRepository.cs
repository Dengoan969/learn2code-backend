using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Infrastructure.Data.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _context;

    public CourseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Course?> GetByIdAsync(Guid id)
    {
        return await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Course>> GetAllAsync()
    {
        return await _context.Courses
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Course>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.Courses
            .Where(c => c.TeacherId == teacherId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Course>> GetByStudentIdAsync(Guid studentId)
    {
        return await _context.Courses
            .Where(c => _context.Groups
                .Where(g => g.CourseId == c.Id)
                .SelectMany(g => g.GroupStudents)
                .Any(gs => gs.StudentId == studentId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Course> CreateAsync(Course course)
    {
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();
        return course;
    }

    public async Task UpdateAsync(Course course)
    {
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var course = await _context.Courses.FindAsync(id);
        if (course != null)
        {
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
        }
    }
}