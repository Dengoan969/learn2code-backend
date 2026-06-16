using System.Security.Claims;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Produces("application/json")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(ICourseRepository courseRepository, ILogger<CoursesController> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourseDto>>> GetAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null) return Unauthorized();

        var userIdGuid = Guid.Parse(userId);
        var courses = role switch
        {
            "Admin" => await _courseRepository.GetAllAsync(),
            "Teacher" => await _courseRepository.GetByTeacherIdAsync(userIdGuid),
            "Student" => await _courseRepository.GetByStudentIdAsync(userIdGuid),
            _ => throw new InvalidOperationException($"Unsupported role: {role}")
        };

        var dtos = courses.Select(MapToDto);
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CourseDto>> GetById(Guid id)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null) return Unauthorized();

        var userIdGuid = Guid.Parse(userId);

        var hasAccess = role switch
        {
            "Admin" => true,
            "Teacher" => course.TeacherId == userIdGuid,
            "Student" => await IsStudentEnrolledInCourseAsync(userIdGuid, id),
            _ => false
        };

        if (!hasAccess) return Forbid();

        return Ok(MapToDto(course));
    }

    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create([FromBody] CreateCourseRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userId == null || (role != "Teacher" && role != "Admin"))
            return Forbid();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            TeacherId = Guid.Parse(userId),
            CreatedAt = DateTime.UtcNow
        };

        await _courseRepository.CreateAsync(course);
        _logger.LogInformation("Course {CourseId} created by user {UserId}", course.Id, userId);

        return CreatedAtAction(nameof(GetById), new { id = course.Id }, MapToDto(course));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest request)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || course.TeacherId != Guid.Parse(userId)))
            return Forbid();

        if (request.Title != null) course.Title = request.Title;
        if (request.Description != null) course.Description = request.Description;

        await _courseRepository.UpdateAsync(course);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || course.TeacherId != Guid.Parse(userId)))
            return Forbid();

        await _courseRepository.DeleteAsync(id);
        return NoContent();
    }

    private async Task<bool> IsStudentEnrolledInCourseAsync(Guid studentId, Guid courseId)
    {
        var studentCourses = await _courseRepository.GetByStudentIdAsync(studentId);
        return studentCourses.Any(c => c.Id == courseId);
    }

    private static CourseDto MapToDto(Course course)
    {
        return new CourseDto(
            course.Id,
            course.Title,
            course.Description,
            course.TeacherId,
            course.CreatedAt
        );
    }
}