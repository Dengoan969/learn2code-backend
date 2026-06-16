using System.Security.Claims;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Mappings;
using Learn2Code.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILessonRepository _lessonRepository;
    private readonly ISandboxClient _sandboxClient;
    private readonly ITaskRepository _taskRepository;

    public TasksController(
        ITaskRepository taskRepository,
        ISandboxClient sandboxClient,
        ICourseRepository courseRepository,
        ILessonRepository lessonRepository)
    {
        _taskRepository = taskRepository;
        _sandboxClient = sandboxClient;
        _courseRepository = courseRepository;
        _lessonRepository = lessonRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskDto>>> GetAll([FromQuery] Guid? lessonId = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null) return Unauthorized();

        var userIdGuid = Guid.Parse(userId);
        IEnumerable<EducationalTask> tasks;

        if (lessonId.HasValue)
        {
            tasks = await _taskRepository.GetByLessonIdAsync(lessonId.Value);

            var lessonTasks = tasks.ToList();
            if (lessonTasks.Any())
            {
                var firstTask = lessonTasks.First();
                var course = await _courseRepository.GetByIdAsync(firstTask.Lesson.CourseId);
                if (course == null) return NotFound("Course not found");

                var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
                if (!hasAccess) return Forbid();
            }
        }
        else
        {
            tasks = await _taskRepository.GetAllAsync();

            var filteredTasks = new List<EducationalTask>();
            foreach (var task in tasks)
            {
                var course = await _courseRepository.GetByIdAsync(task.Lesson.CourseId);
                if (course == null) continue;

                var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
                if (hasAccess) filteredTasks.Add(task);
            }

            tasks = filteredTasks;
        }

        if (role == "Student") tasks = tasks.Where(t => t.PipelineState == TaskPipelineState.Published);

        var dtos = tasks.Select(MapToDto);
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null) return Unauthorized();

        var userIdGuid = Guid.Parse(userId);

        var course = await _courseRepository.GetByIdAsync(task.Lesson.CourseId);
        if (course == null) return NotFound("Course not found");

        var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
        if (!hasAccess) return NotFound();

        if (role == "Student" && task.PipelineState != TaskPipelineState.Published)
            return NotFound();

        return Ok(MapToDto(task));
    }

    [HttpPost("draft")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TaskDto>> CreateDraft([FromBody] CreateTaskDraftRequest request)
    {
        var lesson = await _lessonRepository.GetByIdAsync(request.LessonId!.Value);
        if (lesson == null)
            return NotFound($"Lesson with id {request.LessonId} not found");

        var existingTasks = await _taskRepository.GetByLessonIdAsync(request.LessonId.Value);
        if (existingTasks.Any(t => t.Order == request.Order))
            return BadRequest($"Task with order {request.Order} already exists in this lesson");

        var task = new EducationalTask
        {
            Id = Guid.NewGuid(),
            LessonId = request.LessonId.Value,
            Order = request.Order,
            Title = $"Задание {request.Order}",
            Description = "",
            PipelineState = TaskPipelineState.Draft,
            Config = new TaskConfig(),
            InitialState = new SceneState()
        };

        await _taskRepository.CreateAsync(task);
        var dto = MapToDto(task);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, dto);
    }

    [HttpPost("{id}/test-solution")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TestSolutionResponse>> TestSolution(
        Guid id, [FromBody] TestSolutionRequest request)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound($"Task with id {id} not found");

        if (task.PipelineState != TaskPipelineState.Draft)
            return BadRequest("Only draft tasks can be tested");

        var executionResult = await _sandboxClient.ExecuteAsync(
            request.Code,
            SceneStateMapper.ToModel(request.InitialState),
            TaskConfigMapper.ToModel(request.Config));

        return Ok(new TestSolutionResponse(
            SceneStateMapper.ToDto(executionResult.FinalState),
            executionResult.Success,
            executionResult.Error
        ));
    }


    [HttpPost("{id}/publish")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TaskDto>> Publish(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        if (task.PipelineState == TaskPipelineState.Published)
            return BadRequest("Task is already published");

        if (string.IsNullOrEmpty(task.SolutionCode))
            return BadRequest("Task must have solution code");

        if (task.ExpectedFinalState == null || task.SolutionTrace == null)
            return BadRequest("Task is missing solution data (run preview first)");

        task.PipelineState = TaskPipelineState.Published;
        await _taskRepository.UpdateAsync(task);

        return Ok(MapToDto(task));
    }

    [HttpPost("{id}/unpublish")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TaskDto>> Unpublish(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        if (task.PipelineState != TaskPipelineState.Published)
            return BadRequest("Only published tasks can be unpublished");

        task.PipelineState = TaskPipelineState.Draft;
        await _taskRepository.UpdateAsync(task);

        return Ok(MapToDto(task));
    }

    [HttpPost("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId == null) return Unauthorized();

        var userIdGuid = Guid.Parse(userId);

        var course = await _courseRepository.GetByIdAsync(task.Lesson.CourseId);
        if (course == null) return NotFound("Course not found");

        var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
        if (!hasAccess) return NotFound();

        if (task.PipelineState != TaskPipelineState.Draft)
            return BadRequest("Only draft tasks can be updated");

        if (request.Title != null) task.Title = request.Title;
        if (request.Description != null) task.Description = request.Description;
        if (request.Order.HasValue) task.Order = request.Order.Value;
        if (request.Config != null) task.Config = TaskConfigMapper.ToModel(request.Config);
        if (request.InitialState != null) task.InitialState = SceneStateMapper.ToModel(request.InitialState);

        if (request.InitialState != null || !string.IsNullOrEmpty(request.SolutionCode))
        {
            var executionResult = await _sandboxClient.ExecuteAsync(
                request.SolutionCode ?? task.SolutionCode ?? "", task.InitialState, task.Config);

            Console.WriteLine($"[DEBUG Update] Execution success: {executionResult.Success}");
            if (!executionResult.Success)
            {
                Console.WriteLine($"[DEBUG Update] Execution error: {executionResult.Error}");
                return BadRequest($"Solution execution failed: {executionResult.Error}");
            }

            Console.WriteLine(
                $"[DEBUG Update] Final state sprites count: {executionResult.FinalState?.Sprites?.Count ?? 0}");
            if (executionResult.FinalState?.Sprites?.Count > 0)
            {
                var cat = executionResult.FinalState.Sprites.OfType<CatState>().FirstOrDefault();
                if (cat != null)
                    Console.WriteLine(
                        $"[DEBUG Update] Cat final: X={cat.X}, Y={cat.Y}, Direction={cat.Direction}, Width={cat.Width}, Height={cat.Height}");
            }

            task.SolutionCode = request.SolutionCode;
            task.SolutionTrace = executionResult.Trace;
            task.ExpectedFinalState = executionResult.FinalState;
        }

        await _taskRepository.UpdateAsync(task);
        return Ok(MapToDto(task));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _taskRepository.DeleteAsync(id);
        return NoContent();
    }

    private bool IsAdminOrTeacher()
    {
        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        return currentUserRole == "Teacher" || currentUserRole == "Admin";
    }

    private async Task<bool> CheckCourseAccessAsync(Guid userId, string? role, Guid courseId)
    {
        if (role == "Admin") return true;

        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null) return false;

        if (role == "Teacher") return course.TeacherId == userId;

        if (role == "Student")
        {
            var studentCourses = await _courseRepository.GetByStudentIdAsync(userId);
            return studentCourses.Any(c => c.Id == courseId);
        }

        return false;
    }

    private static TaskDto MapToDto(EducationalTask task)
    {
        return new TaskDto(
            task.Id,
            task.Title ?? "",
            task.Description ?? "",
            task.Order,
            task.LessonId,
            task.PipelineState,
            SceneStateMapper.ToDto(task.InitialState),
            task.ExpectedFinalState != null ? SceneStateMapper.ToDto(task.ExpectedFinalState) : null,
            task.SolutionTrace != null ? ExecutionTraceMapper.ToDto(task.SolutionTrace) : null,
            TaskConfigMapper.ToDto(task.Config),
            task.SolutionCode
        );
    }
}