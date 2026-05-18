using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Core.Interfaces;
using Learn2Code.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Learn2Code.Core.Mappings;

namespace Learn2Code.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISandboxClient _sandboxClient;
    private readonly ICourseRepository _courseRepository;
    private readonly ILessonRepository _lessonRepository;

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

    /// <summary>
    ///     Получить все задания (опционально фильтр по lessonId)
    /// </summary>
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
            
            // Проверяем доступ к уроку (и его курсу)
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
            
            // Фильтруем задания по доступу к курсам
            var filteredTasks = new List<EducationalTask>();
            foreach (var task in tasks)
            {
                var course = await _courseRepository.GetByIdAsync(task.Lesson.CourseId);
                if (course == null) continue;
                
                var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
                if (hasAccess)
                {
                    filteredTasks.Add(task);
                }
            }
            tasks = filteredTasks;
        }

        // Если пользователь студент, показываем только опубликованные задания
        if (role == "Student")
        {
            tasks = tasks.Where(t => t.PipelineState == TaskPipelineState.Published);
        }

        var dtos = tasks.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    ///     Получить задание по ID
    /// </summary>
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
        
        // Проверяем доступ к курсу задания
        var course = await _courseRepository.GetByIdAsync(task.Lesson.CourseId);
        if (course == null) return NotFound("Course not found");
        
        var hasAccess = await CheckCourseAccessAsync(userIdGuid, role, course.Id);
        if (!hasAccess) return NotFound();

        // Если пользователь студент и задание не опубликовано, возвращаем 404
        if (role == "Student" && task.PipelineState != TaskPipelineState.Published)
            return NotFound();

        return Ok(MapToDto(task));
    }

    /// <summary>
    ///     Создать черновик задания (минимальный)
    /// </summary>
    [HttpPost("draft")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<TaskDto>> CreateDraft([FromBody] CreateTaskDraftRequest request)
    {
        // Проверяем, что урок существует
        var lesson = await _lessonRepository.GetByIdAsync(request.LessonId!.Value);
        if (lesson == null)
            return NotFound($"Lesson with id {request.LessonId} not found");

        // Проверяем уникальность порядка в рамках урока
        var existingTasks = await _taskRepository.GetByLessonIdAsync(request.LessonId.Value);
        if (existingTasks.Any(t => t.Order == request.Order))
            return BadRequest($"Task with order {request.Order} already exists in this lesson");

        // Создаем задание с минимальными данными
        var task = new EducationalTask
        {
            Id = Guid.NewGuid(),
            LessonId = request.LessonId.Value,
            Order = request.Order,
            Title = $"Задание {request.Order}", // Автогенерация названия
            Description = "",
            PipelineState = TaskPipelineState.Draft,
            Config = new TaskConfig(), // Дефолтная конфигурация
            InitialState = new SceneState() // Пустое начальное состояние
        };

        await _taskRepository.CreateAsync(task);
        var dto = MapToDto(task);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, dto);
    }

    /// <summary>
    ///     Протестировать решение (без сохранения в БД)
    /// </summary>
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

        // Выполняем решение без сохранения в БД
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


    /// <summary>
    ///     Опубликовать задание
    /// </summary>
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

    /// <summary>
    ///     Вернуть задание в черновик для редактирования
    /// </summary>
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
        // Keep solution data for reference, but teacher will need to run preview again
        // before publishing
        
        await _taskRepository.UpdateAsync(task);
        
        return Ok(MapToDto(task));
    }

    /// <summary>
    ///     Обновить задание (черновики) с возможностью выполнения решения
    /// </summary>
    [HttpPost("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        // Проверяем доступ к курсу задания
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
            
            if (!executionResult.Success)
                return BadRequest($"Solution execution failed: {executionResult.Error}");

            task.SolutionCode = request.SolutionCode;
            task.SolutionTrace = executionResult.Trace;
            task.ExpectedFinalState = executionResult.FinalState;
        }

        await _taskRepository.UpdateAsync(task);
        return Ok(MapToDto(task));
    }

    /// <summary>
    ///     Удалить задание
    /// </summary>
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
        
        if (role == "Teacher")
        {
            return course.TeacherId == userId;
        }
        
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