using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class ProgressControllerTests : TestBase
{
    private async Task<Guid> CreateTestCourseAsync(string title = "Test Course for Progress")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createCourseRequest = new CreateCourseRequest(
                title,
                "Description");
            var response = await Client.PostAsJsonAsync("/api/courses", createCourseRequest);
            Assert.That(response.IsSuccessStatusCode, Is.True);
            var course = await response.Content.ReadFromJsonAsync<CourseDto>(JsonOptions);
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

    private async Task<Guid> CreateTestLessonAsync(Guid courseId, string title = "Test Lesson for Progress",
        int order = 1)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createLessonRequest = new CreateLessonRequest(
                title,
                "Lesson Description",
                order,
                courseId);
            var response = await Client.PostAsJsonAsync("/api/lessons", createLessonRequest);
            Assert.That(response.IsSuccessStatusCode, Is.True);
            var lesson = await response.Content.ReadFromJsonAsync<LessonDto>(JsonOptions);
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

    private async Task<Guid> CreateTestTaskAsync(Guid lessonId, string title = "Test Task for Progress")
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
                1);
            var response = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
            Assert.That(response.IsSuccessStatusCode, Is.True);
            var task = await response.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
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

    private async Task<Guid> CreateTestSubmissionAndProgressAsync(Guid taskId, Guid studentId,
        string code = "print('Hello, World!')")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var student = await GetStudentAsync();
            SetBearerToken(student.Token);

            var createDraftResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", new { });
            Assert.That(createDraftResponse.IsSuccessStatusCode, Is.True);

            var updateRequest = new UpdateDraftRequest(code);
            var updateResponse = await Client.PutAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", updateRequest);
            Assert.That(updateResponse.IsSuccessStatusCode, Is.True);

            var submitResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft/submit", new { });
            Assert.That(submitResponse.IsSuccessStatusCode, Is.True);

            return taskId;
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    [Test]
    public async Task Student_CanViewOwnProgress()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/progress/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progressList = await response.Content.ReadFromJsonAsync<List<ProgressDto>>(JsonOptions);
        Assert.That(progressList, Is.Not.Null);
        Assert.That(progressList, Has.Count.AtLeast(1));
        Assert.That(progressList!.Any(p => p.TaskId == progressTaskId), Is.True);
    }

    [Test]
    public async Task Student_CannotViewOtherStudentProgress()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student1 = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student1.Id);

        var student2 = await CreateAdditionalStudentAsync("otherstudent@example.com");
        SetBearerToken(student2.Token);

        var response = await Client.GetAsync($"/api/progress/{student1.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanViewAnyStudentProgress()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/progress/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progressList = await response.Content.ReadFromJsonAsync<List<ProgressDto>>(JsonOptions);
        Assert.That(progressList, Is.Not.Null);
    }

    [Test]
    public async Task Admin_CanViewAnyStudentProgress()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/progress/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progressList = await response.Content.ReadFromJsonAsync<List<ProgressDto>>(JsonOptions);
        Assert.That(progressList, Is.Not.Null);
    }

    [Test]
    public async Task GetProgressForNonExistentStudent_ReturnsEmptyList()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/progress/{nonExistentId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progressList = await response.Content.ReadFromJsonAsync<List<ProgressDto>>(JsonOptions);
        Assert.That(progressList, Is.Not.Null);
        Assert.That(progressList, Is.Empty);
    }

    [Test]
    public async Task Student_CanViewOwnProgressForSpecificTask()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/progress/{student.Id}/tasks/{progressTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progress = await response.Content.ReadFromJsonAsync<ProgressDto>(JsonOptions);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.TaskId, Is.EqualTo(progressTaskId));
        Assert.That(progress.Completed, Is.False);
    }

    [Test]
    public async Task Student_CannotViewOtherStudentProgressForTask()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student1 = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student1.Id);

        var student2 = await CreateAdditionalStudentAsync("otherstudent2@example.com");
        SetBearerToken(student2.Token);

        var response = await Client.GetAsync($"/api/progress/{student1.Id}/tasks/{progressTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanViewStudentProgressForTask()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/progress/{student.Id}/tasks/{progressTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progress = await response.Content.ReadFromJsonAsync<ProgressDto>(JsonOptions);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.TaskId, Is.EqualTo(progressTaskId));
    }

    [Test]
    public async Task GetProgressForNonExistentTask_ReturnsNotFound()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var nonExistentTaskId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/progress/{student.Id}/tasks/{nonExistentTaskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetProgressForTaskWithoutProgress_ReturnsNotFound()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/progress/{student.Id}/tasks/{taskId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Unauthorized_ReturnsUnauthorized()
    {
        ClearAuthorization();
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        var progressTaskId = await CreateTestSubmissionAndProgressAsync(taskId, student.Id);

        ClearAuthorization();
        var response = await Client.GetAsync($"/api/progress/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}