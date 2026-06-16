using System.Net;
using System.Net.Http.Json;
using System.Text;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class TasksControllerTests : TestBase
{
    private async Task<Guid> CreateTestCourseAsync(string title = "Test Course for Tasks")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createCourseRequest = new CreateCourseRequest(title, "Description");
            var courseResponse = await Client.PostAsJsonAsync("/api/courses", createCourseRequest);
            Assert.That(courseResponse.IsSuccessStatusCode, Is.True);
            var course = await courseResponse.Content.ReadFromJsonAsync<CourseDto>();
            return course!.Id;
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    private async Task<Guid> CreateTestLessonAsync(Guid courseId, string title = "Test Lesson for Tasks", int order = 1)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createLessonRequest = new CreateLessonRequest(title, "Lesson Description", order, courseId);
            var lessonResponse = await Client.PostAsJsonAsync("/api/lessons", createLessonRequest);
            Assert.That(lessonResponse.IsSuccessStatusCode, Is.True);
            var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonDto>();
            return lesson!.Id;
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    private async Task<Guid> CreateTestTaskAsync(Guid lessonId, string title = "Test Task", int order = 1)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var config = new TaskConfigDto
            {
                SceneWidth = 20,
                SceneHeight = 20
            };

            var initialState = new SceneStateDto(
                new CatStateDto
                {
                    X = 0,
                    Y = 0,
                    Direction = 90.0,
                    Visible = true,
                    Costume = "default"
                }
            );

            var createTaskRequest = new CreateTaskDraftRequest(
                lessonId,
                order);

            var taskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);

            Assert.That(taskResponse.IsSuccessStatusCode, Is.True);
            var task = await taskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
            return task!.Id;
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    private async Task PublishTaskAsync(Guid taskId)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var config = new TaskConfigDto
            {
                SceneWidth = 1000.0,
                SceneHeight = 1000.0
            };

            var initialState = new SceneStateDto(
                new CatStateDto
                {
                    X = 100.0,
                    Y = 100.0,
                    Width = 50.0,
                    Height = 50.0,
                    Direction = 90.0,
                    Visible = true,
                    Costume = "default"
                }
            );

            var updateRequest = new UpdateTaskRequest(
                "Test Task",
                "Test Description",
                1,
                config,
                initialState,
                "move(5)"
            );

            var updateResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);

            if (!updateResponse.IsSuccessStatusCode)
            {
                var errorContent = await updateResponse.Content.ReadAsStringAsync();
                Assert.That(updateResponse.IsSuccessStatusCode, Is.True,
                    $"Не удалось обновить задание с решением. Status: {updateResponse.StatusCode}. Error: {errorContent}");
            }

            var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);

            Assert.That(publishResponse.IsSuccessStatusCode, Is.True, "Не удалось опубликовать задание");
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    private async Task<Guid> CreateAndPublishTaskAsync(Guid lessonId, string title = "Published Task", int order = 1)
    {
        var taskId = await CreateTestTaskAsync(lessonId, title, order);
        await PublishTaskAsync(taskId);
        return taskId;
    }

    [Test]
    public async Task Teacher_CanGetTasksByLesson_FromOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var response = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Teacher_CanGetAllTasks_FromOwnCoursesOnly()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var response = await Client.GetAsync("/api/tasks");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Teacher_CanGetTaskById_FromOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var response = await Client.GetAsync($"/api/tasks/{taskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var task = await response.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task, Is.Not.Null);
        Assert.That(task!.Id, Is.EqualTo(taskId));
        Assert.That(task!.Title, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Teacher_CanCreateTask_InOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var config = new TaskConfigDto
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0
        };

        var initialState = new SceneStateDto(
            new CatStateDto
            {
                X = 100.0,
                Y = 100.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var request = new CreateTaskDraftRequest(
            lessonId,
            2);

        var response = await Client.PostAsJsonAsync("/api/tasks/draft", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var task = await response.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task, Is.Not.Null);
        Assert.That(string.IsNullOrEmpty(task!.Title), Is.False);
        Assert.That(task!.LessonId, Is.EqualTo(lessonId));
        Assert.That(task!.Order, Is.EqualTo(2));
    }

    [Test]
    public async Task Teacher_CanUpdateTask_InOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var updateRequest = new UpdateTaskRequest(
            "Updated Title",
            "Updated Description",
            3,
            null,
            null,
            null);

        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getResponse = await Client.GetAsync($"/api/tasks/{taskId}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task!.Title, Is.EqualTo("Updated Title"));
        Assert.That(task!.Description, Is.EqualTo("Updated Description"));
        Assert.That(task!.Order, Is.EqualTo(3));
    }

    [Test]
    public async Task Teacher_CanDeleteTask_InOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId, "Task to Delete", 99);

        var deleteResponse = await Client.DeleteAsync($"/api/tasks/{taskId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/tasks/{taskId}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Admin_CanCreateTask_InAnyCourse()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var config = new TaskConfigDto
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0
        };

        var initialState = new SceneStateDto(
            new CatStateDto
            {
                X = 100.0,
                Y = 100.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var request = new CreateTaskDraftRequest(
            lessonId,
            4);

        var response = await Client.PostAsJsonAsync("/api/tasks/draft", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Admin_CanUpdateTask_InAnyCourse()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var updateRequest = new UpdateTaskRequest("Admin Updated", null, null, null, null, null);
        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Admin_CanDeleteTask_InAnyCourse()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId, "Task for Admin Delete", 100);

        var deleteResponse = await Client.DeleteAsync($"/api/tasks/{taskId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Student_CannotCreateTask_AnyCourse()
    {
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var config = new TaskConfigDto
        {
            SceneWidth = 1000.0,
            SceneHeight = 1000.0
        };

        var initialState = new SceneStateDto(
            new CatStateDto
            {
                X = 0.0,
                Y = 0.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }
        );

        var request = new CreateTaskDraftRequest(
            lessonId,
            5);

        var response = await Client.PostAsJsonAsync("/api/tasks/draft", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CannotUpdateTask_AnyCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var updateRequest = new UpdateTaskRequest("Student Updated", null, null, null, null, null);
        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CannotDeleteTask_AnyCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var response = await Client.DeleteAsync($"/api/tasks/{taskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CanViewPublishedTasks_FromEnrolledCoursesOnly()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updateRequest = new UpdateTaskRequest(
            "Test Task",
            "Test Description",
            1,
            new TaskConfigDto { SceneWidth = 1000.0, SceneHeight = 1000.0 },
            new SceneStateDto(new CatStateDto
            {
                X = 100.0,
                Y = 100.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }),
            "move(5)"
        );
        var updateResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest, JsonOptions);
        Assert.That(updateResponse.IsSuccessStatusCode, Is.True, "Не удалось обновить задание с решением");

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.IsSuccessStatusCode, Is.True, "Не удалось опубликовать задание");

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(1), "Студент должен видеть опубликованное задание");
        Assert.That(tasks[0].Id, Is.EqualTo(taskId));
    }

    [Test]
    public async Task GetTask_ReturnsNotFound_ForNonExistentId()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/tasks/{nonExistentId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateTask_ValidatesRequiredFields()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var invalidRequest1 = new
        {
            Order = 1
        };

        var response1 = await Client.PostAsJsonAsync("/api/tasks/draft", invalidRequest1);
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Should fail without LessonId");

        var invalidRequest2 = new
        {
            LessonId = lessonId,
            Order = 0
        };

        var response2 = await Client.PostAsJsonAsync("/api/tasks/draft", invalidRequest2);
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Should fail with Order <= 0");
    }

    [Test]
    public async Task UpdateTask_ValidatesOrderRange()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var updateRequest = new UpdateTaskRequest(null, null, 0, null, null, null);
        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        updateRequest = new UpdateTaskRequest(null, null, -5, null, null, null);
        response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        updateRequest = new UpdateTaskRequest(null, null, 10, null, null, null);
        response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Teacher_CanSeeUnpublishedTasks_FromOwnCourses()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var unpublishedTaskId = await CreateTestTaskAsync(lessonId, "Unpublished Task");

        var publishedTaskId = await CreateAndPublishTaskAsync(lessonId, "Published Task", 2);

        var response = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(2), "Teacher should see both published and unpublished tasks");

        var taskIds = tasks!.Select(t => t.Id).ToList();
        Assert.That(taskIds, Contains.Item(unpublishedTaskId), "Teacher should see unpublished task");
        Assert.That(taskIds, Contains.Item(publishedTaskId), "Teacher should see published task");
    }

    [Test]
    public async Task Admin_CanSeeUnpublishedTasks_FromAnyCourse()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var unpublishedTaskId = await CreateTestTaskAsync(lessonId, "Unpublished Task");

        var publishedTaskId = await CreateAndPublishTaskAsync(lessonId, "Published Task", 2);

        var response = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(2), "Admin should see both published and unpublished tasks");

        var taskIds = tasks!.Select(t => t.Id).ToList();
        Assert.That(taskIds, Contains.Item(unpublishedTaskId), "Admin should see unpublished task");
        Assert.That(taskIds, Contains.Item(publishedTaskId), "Admin should see published task");
    }

    [Test]
    public async Task Student_CanOnlySeePublishedTasks_FromEnrolledCourses()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var unpublishedTaskId = await CreateTestTaskAsync(lessonId, "Unpublished Task");

        var publishedTaskId = await CreateAndPublishTaskAsync(lessonId, "Published Task", 2);

        SetBearerToken(student.Token);

        var response = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(1), "Student should see only published tasks");
        Assert.That(tasks![0].Id, Is.EqualTo(publishedTaskId), "Student should see published task");
        Assert.That(tasks[0].PipelineState, Is.EqualTo(TaskPipelineState.Published), "Task should be published");
    }

    [Test]
    public async Task Student_CannotAccessUnpublishedTask_EvenIfEnrolled()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var unpublishedTaskId = await CreateTestTaskAsync(lessonId, "Unpublished Task");

        SetBearerToken(student.Token);

        var response = await Client.GetAsync($"/api/tasks/{unpublishedTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Student_CanAccessPublishedTask_IfEnrolledInCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishedTaskId = await CreateAndPublishTaskAsync(lessonId);

        SetBearerToken(student.Token);

        var response = await Client.GetAsync($"/api/tasks/{publishedTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var task = await response.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task, Is.Not.Null);
        Assert.That(task!.Id, Is.EqualTo(publishedTaskId));
        Assert.That(task!.PipelineState, Is.EqualTo(TaskPipelineState.Published));
    }

    [Test]
    public async Task MixedPublishedUnpublishedTasks_FilterCorrectly_ByRoleAndCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var unpublished1 = await CreateTestTaskAsync(lessonId, "Unpublished 1");
        var unpublished2 = await CreateTestTaskAsync(lessonId, "Unpublished 2", 2);
        var published1 = await CreateAndPublishTaskAsync(lessonId, "Published 1", 3);
        var published2 = await CreateAndPublishTaskAsync(lessonId, "Published 2", 4);

        var teacherResponse = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        var teacherTasks = await teacherResponse.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(teacherTasks, Has.Count.EqualTo(4), "Teacher should see all 4 tasks");

        SetBearerToken(student.Token);

        var studentResponse = await Client.GetAsync($"/api/tasks?lessonId={lessonId}");
        var studentTasks = await studentResponse.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(studentTasks, Has.Count.EqualTo(2), "Student should see only 2 published tasks");

        var studentTaskIds = studentTasks!.Select(t => t.Id).ToList();
        Assert.That(studentTaskIds, Contains.Item(published1));
        Assert.That(studentTaskIds, Contains.Item(published2));
        Assert.That(studentTaskIds, Does.Not.Contain(unpublished1));
        Assert.That(studentTaskIds, Does.Not.Contain(unpublished2));
    }

    [Test]
    public async Task TaskPipeline_CanPublishAndUnpublish_ByTeacher()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var getResponse = await Client.GetAsync($"/api/tasks/{taskId}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task!.PipelineState, Is.EqualTo(TaskPipelineState.Draft));

        var updateRequest = new UpdateTaskRequest(
            "Test Task",
            "Test Description",
            1,
            new TaskConfigDto { SceneWidth = 1000.0, SceneHeight = 1000.0 },
            new SceneStateDto(new CatStateDto
            {
                X = 100.0,
                Y = 100.0,
                Width = 50.0,
                Height = 50.0,
                Direction = 90.0,
                Visible = true,
                Costume = "default"
            }),
            "move(5)"
        );
        var updateResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}", updateRequest);
        Assert.That(updateResponse.IsSuccessStatusCode, Is.True, "Failed to update task with solution");

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.IsSuccessStatusCode, Is.True);

        getResponse = await Client.GetAsync($"/api/tasks/{taskId}");
        task = await getResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task!.PipelineState, Is.EqualTo(TaskPipelineState.Published));

        var unpublishResponse = await Client.PostAsync($"/api/tasks/{taskId}/unpublish", null);
        Assert.That(unpublishResponse.IsSuccessStatusCode, Is.True);

        getResponse = await Client.GetAsync($"/api/tasks/{taskId}");
        task = await getResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        Assert.That(task!.PipelineState, Is.EqualTo(TaskPipelineState.Draft));
    }

    [Test]
    public async Task Teacher_CannotSeeTasks_FromOtherTeachersCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId1 = await CreateTestCourseAsync("Course by Teacher 1");
        var lessonId1 = await CreateTestLessonAsync(courseId1);
        var taskId1 = await CreateTestTaskAsync(lessonId1, "Task by Teacher 1");

        var teacher2 = await CreateAdditionalTeacherAsync("teacher2@test.com");
        SetBearerToken(teacher2.Token);

        var response = await Client.GetAsync("/api/tasks");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(0), "Teacher should not see tasks from other teacher's course");
    }

    [Test]
    public async Task Teacher_CannotUpdateTask_FromOtherTeachersCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId1 = await CreateTestCourseAsync("Course by Teacher 1");
        var lessonId1 = await CreateTestLessonAsync(courseId1);
        var taskId1 = await CreateTestTaskAsync(lessonId1, "Task by Teacher 1");

        var teacher2 = await CreateAdditionalTeacherAsync("teacher2@test.com");
        SetBearerToken(teacher2.Token);

        var updateRequest = new UpdateTaskRequest("Updated Title", "Updated description", 2, null, null, null);
        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId1}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "Teacher should get 404 when trying to update task from other teacher's course");
    }

    [Test]
    public async Task Student_CannotSeeTasks_FromUnenrolledCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateAndPublishTaskAsync(lessonId);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var response = await Client.GetAsync("/api/tasks");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.EqualTo(0), "Student should not see tasks from unenrolled course");
    }

    [Test]
    public async Task Admin_CanSeeTasks_FromAnyCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId1 = await CreateTestCourseAsync("Course 1");
        var lessonId1 = await CreateTestLessonAsync(courseId1);
        var taskId1 = await CreateTestTaskAsync(lessonId1, "Task 1");

        var teacher2 = await CreateAdditionalTeacherAsync("teacher2@test.com");
        SetBearerToken(teacher2.Token);
        var courseId2 = await CreateTestCourseAsync("Course 2");
        var lessonId2 = await CreateTestLessonAsync(courseId2);
        var taskId2 = await CreateTestTaskAsync(lessonId2, "Task 2");

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync("/api/tasks");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>(JsonOptions);
        Assert.That(tasks, Is.Not.Null);
        Assert.That(tasks, Has.Count.AtLeast(2), "Admin should see tasks from any course");

        var taskIds = tasks.Select(t => t.Id).ToList();
        Assert.That(taskIds, Contains.Item(taskId1));
        Assert.That(taskIds, Contains.Item(taskId2));
    }

    [Test]
    public async Task GetTask_ReturnsNotFound_ForTaskInInaccessibleCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var response = await Client.GetAsync($"/api/tasks/{taskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TestSolution_WithJsonPayload_DeserializesCorrectly()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var jsonPayload = @"{
            ""code"": ""move(10)"",
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""width"": 50.0,
                        ""height"": 50.0,
                        ""visible"": true,
                        ""direction"": 90.0,
                        ""costume"": ""default"",
                        ""saidTexts"": {},
                        ""collectedItems"": {}
                    },
                    {
                        ""type"": ""apple"",
                        ""x"": 250.0,
                        ""y"": 250.0,
                        ""width"": 50.0,
                        ""height"": 50.0,
                        ""visible"": true
                    },
                    {
                        ""type"": ""wall"",
                        ""x"": 500.0,
                        ""y"": 500.0,
                        ""width"": 50.0,
                        ""height"": 50.0,
                        ""visible"": true
                    }
                ]
            },
            ""config"": {
                ""sceneWidth"": 1000.0,
                ""sceneHeight"": 1000.0
            }
        }";

        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync($"/api/tasks/{taskId}/test-solution", content);

        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
            $"Should not get internal server error due to deserialization. Response: {responseContent}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<TestSolutionResponse>(JsonOptions);
            Assert.That(result, Is.Not.Null);
        }
        else
        {
            Assert.That(responseContent,
                Does.Not.Contain("Deserialization of types without a parameterless constructor"),
                "Should not have deserialization error");
            Assert.That(responseContent, Does.Not.Contain("SpriteStateDto"),
                "Should not have SpriteStateDto deserialization error");
        }
    }
}