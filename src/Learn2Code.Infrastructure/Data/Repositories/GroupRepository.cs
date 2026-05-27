using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Infrastructure.Data.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _context;

    public GroupRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Group?> GetByIdAsync(Guid id)
    {
        return await _context.Groups
            .Include(g => g.GroupStudents)
            .ThenInclude(gs => gs.Student)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<IEnumerable<Group>> GetAllAsync()
    {
        return await _context.Groups
            .Include(g => g.GroupStudents)
            .ThenInclude(gs => gs.Student)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Group>> GetByCourseIdAsync(Guid courseId)
    {
        return await _context.Groups
            .Include(g => g.GroupStudents)
            .ThenInclude(gs => gs.Student)
            .Where(g => g.CourseId == courseId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Group>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.Groups
            .Include(g => g.GroupStudents)
            .ThenInclude(gs => gs.Student)
            .Where(g => g.TeacherId == teacherId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Group>> GetByStudentIdAsync(Guid studentId)
    {
        var groupIds = await _context.GroupStudents
            .Where(gs => gs.StudentId == studentId)
            .Select(gs => gs.GroupId)
            .ToListAsync();

        if (groupIds.Count == 0) return new List<Group>();

        return await _context.Groups
            .Include(g => g.GroupStudents)
            .ThenInclude(gs => gs.Student)
            .Where(g => groupIds.Contains(g.Id))
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Group> CreateAsync(Group group)
    {
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task UpdateAsync(Group group)
    {
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var group = await _context.Groups.FindAsync(id);
        if (group != null)
        {
            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddStudentToGroupAsync(Guid groupId, Guid studentId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) throw new InvalidOperationException($"Group with id {groupId} not found");

        var student = await _context.Users.FindAsync(studentId);
        if (student == null) throw new InvalidOperationException($"Student with id {studentId} not found");

        var existingMembership = await _context.GroupStudents
            .FirstOrDefaultAsync(gs => gs.GroupId == groupId && gs.StudentId == studentId);

        if (existingMembership != null) return;

        var groupStudent = new GroupStudent
        {
            GroupId = groupId,
            StudentId = studentId,
            JoinedAt = DateTime.UtcNow
        };

        _context.GroupStudents.Add(groupStudent);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveStudentFromGroupAsync(Guid groupId, Guid studentId)
    {
        var groupStudent = await _context.GroupStudents
            .FirstOrDefaultAsync(gs => gs.GroupId == groupId && gs.StudentId == studentId);

        if (groupStudent != null)
        {
            _context.GroupStudents.Remove(groupStudent);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsStudentInGroupAsync(Guid groupId, Guid studentId)
    {
        return await _context.GroupStudents
            .AnyAsync(gs => gs.GroupId == groupId && gs.StudentId == studentId);
    }

    public async Task<IEnumerable<User>> GetStudentsInGroupAsync(Guid groupId)
    {
        return await _context.GroupStudents
            .Where(gs => gs.GroupId == groupId)
            .Join(_context.Users,
                gs => gs.StudentId,
                u => u.Id,
                (gs, u) => u)
            .ToListAsync();
    }
}