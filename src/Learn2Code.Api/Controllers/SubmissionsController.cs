using System.Security.Claims;
using System.Text.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Learn2Code.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/[controller]")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionRepository _submissions;
    private readonly SubmissionService _submissionService;

    public SubmissionsController(
        SubmissionService submissionService,
        ISubmissionRepository submissions)
    {
        _submissionService = submissionService;
        _submissions = submissions;
    }

    /// <summary>
    ///     Получить результат конкретной попытки (только свои или для преподавателей/админов)
    /// </summary>
    [HttpGet("{submissionId}")]
    public async Task<ActionResult<SubmissionDto>> GetById(Guid taskId, Guid submissionId)
    {
        var submission = await _submissions.GetByIdAsync(submissionId);
        if (submission == null || submission.TaskId != taskId)
            return NotFound();

        if (!CanViewSubmission(submission))
            return Forbid();

        var result = ParseResult(submission.ResultJson);
        return Ok(MapToDto(submission, result));
    }

    /// <summary>
    ///     Все попытки по заданию (только свои или для преподавателей/админов)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SubmissionDto>>> GetAll(
        Guid taskId,
        [FromQuery] string? studentId = null)
    {
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var isTeacherOrAdmin = currentUserRole == "Teacher" || currentUserRole == "Admin";

        IEnumerable<Submission> submissions;

        if (!string.IsNullOrEmpty(studentId) && Guid.TryParse(studentId, out var sId))
        {
            if (!isTeacherOrAdmin && currentUserIdClaim != sId.ToString())
                return Forbid();

            submissions = await _submissions.GetByStudentAndTaskAsync(sId, taskId);
        }
        else
        {
            if (isTeacherOrAdmin)
            {
                submissions = await _submissions.GetAllByTaskIdAsync(taskId);
            }
            else
            {
                if (string.IsNullOrEmpty(currentUserIdClaim) ||
                    !Guid.TryParse(currentUserIdClaim, out var currentUserId))
                    return Unauthorized("Не удалось определить идентификатор студента");

                submissions = await _submissions.GetByStudentAndTaskAsync(currentUserId, taskId);
            }
        }

        var dtos = submissions.Select(s => MapToDto(s, ParseResult(s.ResultJson)));
        return Ok(dtos);
    }

    /// <summary>
    ///     Получить черновик решения для задания
    /// </summary>
    [HttpGet("draft")]
    public async Task<ActionResult<SubmissionDto>> GetDraft(Guid taskId)
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized("Не удалось определить идентификатор студента");

        var draft = await _submissionService.GetDraftAsync(taskId, studentId.Value);
        if (draft == null)
            return NotFound("Черновик не найден");

        return MapToDto(draft, ParseResult(draft.ResultJson));
    }

    /// <summary>
    ///     Создать новый черновик решения
    /// </summary>
    [HttpPost("draft")]
    public async Task<ActionResult<SubmissionDto>> CreateDraft(Guid taskId)
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized("Не удалось определить идентификатор студента");

        try
        {
            var draft = await _submissionService.CreateDraftAsync(taskId, studentId.Value);
            return CreatedAtAction(nameof(GetDraft), new { taskId }, MapToDto(draft, null));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    ///     Обновить черновик решения
    /// </summary>
    [HttpPut("draft")]
    public async Task<ActionResult<SubmissionDto>> UpdateDraft(
        Guid taskId,
        [FromBody] UpdateDraftRequest request)
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized("Не удалось определить идентификатор студента");

        try
        {
            var draft = await _submissionService.UpdateDraftAsync(taskId, studentId.Value, request);
            return MapToDto(draft, ParseResult(draft.ResultJson));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    ///     Отправить черновик на проверку
    /// </summary>
    [HttpPost("draft/submit")]
    public async Task<ActionResult<SubmissionDto>> SubmitDraft(Guid taskId)
    {
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized("Не удалось определить идентификатор студента");

        try
        {
            var submission = await _submissionService.SubmitDraftAsync(taskId, studentId.Value);
            var result = ParseResult(submission.ResultJson);
            return MapToDto(submission, result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private static SubmissionDto MapToDto(Submission s, CheckResultDto? result)
    {
        return new SubmissionDto(
            s.Id,
            s.TaskId,
            s.StudentId.ToString(),
            s.Code,
            s.SubmittedAt,
            result,
            s.IsDraft
        );
    }

    private static CheckResultDto? ParseResult(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<CheckResultDto>(json, Learn2Code.Core.JsonOptions.Default);
        }
        catch
        {
            return null;
        }
    }

    private Guid? GetStudentId()
    {
        var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(studentIdClaim) || !Guid.TryParse(studentIdClaim, out var studentId))
            return null;
        return studentId;
    }

    /// <summary>
    ///     Проверяет, может ли текущий пользователь просматривать указанную попытку
    /// </summary>
    private bool CanViewSubmission(Submission submission)
    {
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdClaim))
            return false;

        // Пользователь может просматривать свою собственную попытку
        if (currentUserIdClaim == submission.StudentId.ToString())
            return true;

        // Преподаватели и админы могут просматривать любые попытки
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        return currentUserRole == "Teacher" || currentUserRole == "Admin";
    }
}