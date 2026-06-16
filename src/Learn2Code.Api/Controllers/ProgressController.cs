using System.Security.Claims;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProgressController : ControllerBase
{
    private readonly IProgressRepository _progressRepo;
    private readonly ITaskRepository _taskRepo;

    public ProgressController(IProgressRepository progressRepo, ITaskRepository taskRepo)
    {
        _progressRepo = progressRepo;
        _taskRepo = taskRepo;
    }

    [HttpGet("{studentId}")]
    public async Task<ActionResult<IEnumerable<ProgressDto>>> GetByStudent(Guid studentId)
    {
        if (!CanViewProgress(studentId))
            return Forbid();

        var progressList = await _progressRepo.GetByStudentIdAsync(studentId);
        var dtos = new List<ProgressDto>();

        foreach (var p in progressList)
        {
            var task = await _taskRepo.GetByIdAsync(p.TaskId);
            dtos.Add(new ProgressDto(
                p.TaskId,
                task?.Title ?? "Unknown",
                p.Completed,
                p.AttemptsCount,
                p.LastAttemptAt
            ));
        }

        return Ok(dtos);
    }

    [HttpGet("{studentId}/tasks/{taskId}")]
    public async Task<ActionResult<ProgressDto>> GetByStudentAndTask(Guid studentId, Guid taskId)
    {
        if (!CanViewProgress(studentId))
            return Forbid();

        var progress = await _progressRepo.GetByStudentAndTaskAsync(studentId, taskId);
        if (progress == null)
            return NotFound();

        var task = await _taskRepo.GetByIdAsync(taskId);
        var dto = new ProgressDto(
            progress.TaskId,
            task?.Title ?? "Unknown",
            progress.Completed,
            progress.AttemptsCount,
            progress.LastAttemptAt
        );
        return Ok(dto);
    }

    private bool CanViewProgress(Guid targetStudentId)
    {
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserIdClaim))
            return false;

        if (currentUserIdClaim == targetStudentId.ToString())
            return true;

        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        return currentUserRole == "Teacher" || currentUserRole == "Admin";
    }
}