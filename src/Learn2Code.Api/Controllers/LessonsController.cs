using System.Security.Claims;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/lessons")]
[Produces("application/json")]
[Authorize]
public class LessonsController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILessonRepository _lessonRepository;
    private readonly ILogger<LessonsController> _logger;

    public LessonsController(
        ILessonRepository lessonRepository,
        ICourseRepository courseRepository,
        ILogger<LessonsController> logger)
    {
        _lessonRepository = lessonRepository;
        _courseRepository = courseRepository;
        _logger = logger;
    }

    /// <summary>
    ///     Получить все уроки курса
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LessonDto>>> GetByCourse([FromQuery] Guid courseId)
    {
        if (courseId == Guid.Empty)
            return BadRequest("courseId is required");

        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null) return NotFound("Course not found");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (userId == null) return Unauthorized();
        
        var userIdGuid = Guid.Parse(userId);
        
        // Проверяем доступ в зависимости от роли
        var hasAccess = role switch
        {
            "Admin" => true,
            "Teacher" => course.TeacherId == userIdGuid,
            "Student" => await IsStudentEnrolledInCourseAsync(userIdGuid, courseId),
            _ => false
        };

        if (!hasAccess) return Forbid();

        var lessons = await _lessonRepository.GetByCourseIdAsync(courseId);
        var dtos = lessons.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    ///     Получить урок по ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<LessonDto>> GetById(Guid id)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id);
        if (lesson == null) return NotFound();

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course == null) return NotFound("Course not found");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        if (userId == null) return Unauthorized();
        
        var userIdGuid = Guid.Parse(userId);
        
        // Проверяем доступ в зависимости от роли
        var hasAccess = role switch
        {
            "Admin" => true,
            "Teacher" => course.TeacherId == userIdGuid,
            "Student" => await IsStudentEnrolledInCourseAsync(userIdGuid, course.Id),
            _ => false
        };

        if (!hasAccess) return Forbid();

        return Ok(MapToDto(lesson));
    }

    /// <summary>
    ///     Создать новый урок (только учитель-владелец курса или админ)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LessonDto>> Create([FromBody] CreateLessonRequest request)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course == null) return NotFound("Course not found");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || course.TeacherId != Guid.Parse(userId)))
            return Forbid();

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Order = request.Order,
            CourseId = request.CourseId
        };

        await _lessonRepository.CreateAsync(lesson);
        _logger.LogInformation("Lesson {LessonId} created in course {CourseId} by user {UserId}",
            lesson.Id, lesson.CourseId, userId);

        return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, MapToDto(lesson));
    }

    /// <summary>
    ///     Обновить урок (только учитель-владелец курса или админ)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLessonRequest request)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id);
        if (lesson == null) return NotFound();

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course == null) return NotFound("Course not found");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || course.TeacherId != Guid.Parse(userId)))
            return Forbid();

        if (request.Title != null) lesson.Title = request.Title;
        if (request.Description != null) lesson.Description = request.Description;
        if (request.Order.HasValue) lesson.Order = request.Order.Value;

        await _lessonRepository.UpdateAsync(lesson);
        return NoContent();
    }

    /// <summary>
    ///     Удалить урок (только учитель-владелец курса или админ)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var lesson = await _lessonRepository.GetByIdAsync(id);
        if (lesson == null) return NotFound();

        var course = await _courseRepository.GetByIdAsync(lesson.CourseId);
        if (course == null) return NotFound("Course not found");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != "Admin" && (userId == null || course.TeacherId != Guid.Parse(userId)))
            return Forbid();

        await _lessonRepository.DeleteAsync(id);
        return NoContent();
    }

    private async Task<bool> IsStudentEnrolledInCourseAsync(Guid studentId, Guid courseId)
    {
        var studentCourses = await _courseRepository.GetByStudentIdAsync(studentId);
        return studentCourses.Any(c => c.Id == courseId);
    }

    private static LessonDto MapToDto(Lesson lesson)
    {
        return new LessonDto(
            lesson.Id,
            lesson.Title,
            lesson.Description,
            lesson.Order,
            lesson.CourseId
        );
    }
}