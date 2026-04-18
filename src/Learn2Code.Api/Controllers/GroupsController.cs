using System.Security.Claims;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Produces("application/json")]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly AppDbContext _dbContext;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IGroupRepository groupRepository,
        AppDbContext dbContext,
        ICourseRepository courseRepository,
        ILogger<GroupsController> logger)
    {
        _groupRepository = groupRepository;
        _dbContext = dbContext;
        _courseRepository = courseRepository;
        _logger = logger;
    }

    /// <summary>
    ///     Получить все группы (админ видит все, учитель — только свои)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        IEnumerable<Group> groups;
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            groups = await _groupRepository.GetAllAsync();
        }
        else if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            if (userId == null) return Unauthorized();
            groups = await _groupRepository.GetByTeacherIdAsync(Guid.Parse(userId));
        }
        else
        {
            if (userId == null) return Unauthorized();
            groups = await _groupRepository.GetByStudentIdAsync(Guid.Parse(userId));
        }

        var dtos = groups.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    ///     Получить группу по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<GroupDto>> GetById(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (userId == null) return Forbid();
            
            if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                if (group.TeacherId != Guid.Parse(userId)) return Forbid();
            }
            else if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                if (!await _groupRepository.IsStudentInGroupAsync(id, Guid.Parse(userId))) return Forbid();
            }
            else
            {
                // Other roles not allowed
                return Forbid();
            }
        }

        return Ok(MapToDto(group));
    }

    /// <summary>
    ///     Создать новую группу (только учитель или админ)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GroupDto>> Create([FromBody] CreateGroupRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userId == null || (role != "Teacher" && role != "Admin"))
            return Forbid();

        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course == null) return BadRequest("Course not found");

        var teacherId = request.TeacherId;
        if (teacherId != Guid.Empty)
        {
            var teacher = await _dbContext.Users.FindAsync(teacherId);
            if (teacher == null || teacher.Role != UserRole.Teacher)
                return BadRequest("Teacher not found or not a teacher");
        }
        else
        {
            if (role != "Teacher") return BadRequest("TeacherId is required for admin");
            teacherId = Guid.Parse(userId);
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CourseId = request.CourseId,
            TeacherId = teacherId,
            CreatedAt = DateTime.UtcNow
        };

        await _groupRepository.CreateAsync(group);
        _logger.LogInformation("Group {GroupId} created by user {UserId}", group.Id, userId);

        return CreatedAtAction(nameof(GetById), new { id = group.Id }, MapToDto(group));
    }

    /// <summary>
    ///     Обновить группу (только учитель-владелец или админ)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<GroupDto>> Update(Guid id, [FromBody] UpdateGroupRequest request)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && (userId == null || group.TeacherId != Guid.Parse(userId)))
            return Forbid();

        if (request.Name != null) group.Name = request.Name;
        if (request.Description != null) group.Description = request.Description;
        if (request.TeacherId.HasValue)
        {
            var newTeacher = await _dbContext.Users.FindAsync(request.TeacherId.Value);
            if (newTeacher == null || newTeacher.Role != UserRole.Teacher)
                return BadRequest("Teacher not found or not a teacher");
            group.TeacherId = request.TeacherId.Value;
        }

        await _groupRepository.UpdateAsync(group);
        _logger.LogInformation("Group {GroupId} updated by user {UserId}", group.Id, userId);

        return Ok(MapToDto(group));
    }

    /// <summary>
    ///     Удалить группу (только учитель-владелец или админ)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && (userId == null || group.TeacherId != Guid.Parse(userId)))
            return Forbid();

        await _groupRepository.DeleteAsync(id);
        _logger.LogInformation("Group {GroupId} deleted by user {UserId}", id, userId);

        return NoContent();
    }

    /// <summary>
    ///     Добавить студента в группу (только учитель-владелец или админ)
    /// </summary>
    [HttpPost("{id}/students")]
    public async Task<IActionResult> AddStudent(Guid id, [FromBody] AddStudentToGroupRequest request)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || group.TeacherId != Guid.Parse(userId)))
            return Forbid();

        var student = await _dbContext.Users.FindAsync(request.StudentId);
        if (student == null || student.Role != UserRole.Student)
            return BadRequest("Student not found or not a student");

        if (await _groupRepository.IsStudentInGroupAsync(id, request.StudentId))
            return Conflict("Student already in group");

        await _groupRepository.AddStudentToGroupAsync(id, request.StudentId);
        _logger.LogInformation("Student {StudentId} added to group {GroupId} by user {UserId}",
            request.StudentId, id, userId);

        return Ok();
    }

    /// <summary>
    ///     Удалить студента из группы (только учитель-владелец или админ)
    /// </summary>
    [HttpDelete("{id}/students/{studentId}")]
    public async Task<IActionResult> RemoveStudent(Guid id, Guid studentId)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || group.TeacherId != Guid.Parse(userId)))
            return Forbid();

        if (!await _groupRepository.IsStudentInGroupAsync(id, studentId))
            return NotFound("Student not found in group");

        await _groupRepository.RemoveStudentFromGroupAsync(id, studentId);
        _logger.LogInformation("Student {StudentId} removed from group {GroupId} by user {UserId}",
            studentId, id, userId);

        return NoContent();
    }

    /// <summary>
    ///     Получить студентов группы (доступно учителю-владельцу, админу и студентам группы)
    /// </summary>
    [HttpGet("{id}/students")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetStudents(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (userId == null) return Forbid();
            
            if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                if (group.TeacherId != Guid.Parse(userId)) return Forbid();
            }
            else if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                if (!await _groupRepository.IsStudentInGroupAsync(id, Guid.Parse(userId))) return Forbid();
            }
            else
            {
                // Other roles not allowed
                return Forbid();
            }
        }

        var students = await _groupRepository.GetStudentsInGroupAsync(id);
        var dtos = students.Select(u => new UserDto(u.Id, u.Login, u.DisplayName, u.Role.ToString(), u.CreatedAt));
        return Ok(dtos);
    }

    private static GroupDto MapToDto(Group group)
    {
        var studentDtos = group.GroupStudents?
            .Select(gs => new UserDto(
                gs.Student.Id,
                gs.Student.Login,
                gs.Student.DisplayName,
                gs.Student.Role.ToString(),
                gs.Student.CreatedAt))
            .ToList() ?? new List<UserDto>();

        return new GroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.CourseId,
            group.TeacherId,
            group.CreatedAt,
            studentDtos);
    }
}