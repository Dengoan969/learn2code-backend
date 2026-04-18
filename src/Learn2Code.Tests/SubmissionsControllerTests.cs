using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Enums;
using Learn2Code.Core.Models;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class SubmissionsControllerTests : TestBase
{
    private async Task<Guid> CreateTestCourseAsync(string title = "Test Course for Submissions")
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
            var course = await response.Content.ReadFromJsonAsync<CourseDto>();
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

    private async Task<Guid> CreateTestLessonAsync(Guid courseId, string title = "Test Lesson for Submissions", int order = 1)
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
            var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
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

    private async Task<Guid> CreateTestTaskAsync(Guid lessonId, string title = "Test Task")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);
            
            var config = new TaskConfigDto
            {
                GridWidth = 20,
                GridHeight = 20
            };
            
            var initialState = new SceneStateDto(
                new CatStateDto
                {
                    GridX = 0,
                    GridY = 0,
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

    private async Task<Guid> CreateTestSubmissionAsync(Guid taskId, Guid studentId, string code = "print('Hello, World!')")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var student = await GetStudentAsync();
            SetBearerToken(student.Token);
            
            var submitRequest = new SubmitSolutionRequest(
                "python",
                code,
                null,
                null);
            var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions", submitRequest);
            Assert.That(response.IsSuccessStatusCode, Is.True);
            var submission = await response.Content.ReadFromJsonAsync<SubmissionDto>();
            return submission!.Id;
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
    public async Task Student_CanSubmitSolution()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var request = new SubmitSolutionRequest(
            "python",
            "print('Test')",
            null,
            null);

        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await response.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.TaskId, Is.EqualTo(taskId));
        Assert.That(submission!.StudentId, Is.EqualTo(student.Id.ToString()));
        Assert.That(submission!.Code, Is.EqualTo("print('Test')"));
    }

    [Test]
    public async Task Student_CanViewOwnSubmission()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions/{submissionId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await response.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.Id, Is.EqualTo(submissionId));
        Assert.That(submission!.StudentId, Is.EqualTo(student.Id.ToString()));
    }

    [Test]
    public async Task Student_CannotViewOtherStudentSubmission()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student1 = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student1.Id);
        
        var otherStudent = await CreateAdditionalStudentAsync("anotherstudent@example.com");
        SetBearerToken(otherStudent.Token);

        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions/{submissionId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanViewAnySubmission()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions/{submissionId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await response.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.Id, Is.EqualTo(submissionId));
    }

    [Test]
    public async Task Admin_CanViewAnySubmission()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);
        
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions/{submissionId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await response.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.Id, Is.EqualTo(submissionId));
    }

    [Test]
    public async Task Teacher_CanGetAllSubmissionsForTask()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submissions = await response.Content.ReadFromJsonAsync<List<SubmissionDto>>();
        Assert.That(submissions, Is.Not.Null);
        Assert.That(submissions, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Teacher_CanFilterSubmissionsByStudentId()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);

        SetBearerToken(teacher.Token);
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions?studentId={student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submissions = await response.Content.ReadFromJsonAsync<List<SubmissionDto>>();
        Assert.That(submissions, Is.Not.Null);
        Assert.That(submissions, Has.Count.AtLeast(1));
        Assert.That(submissions!.All(s => s.StudentId == student.Id.ToString()), Is.True);
    }

    [Test]
    public async Task Student_CanGetOwnSubmissionsForTask()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submissions = await response.Content.ReadFromJsonAsync<List<SubmissionDto>>();
        Assert.That(submissions, Is.Not.Null);
        Assert.That(submissions, Has.Count.AtLeast(1));
        Assert.That(submissions!.All(s => s.StudentId == student.Id.ToString()), Is.True);
    }

    [Test]
    public async Task Student_CannotFilterByOtherStudentId()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student1 = await GetStudentAsync();
        var submissionId = await CreateTestSubmissionAsync(taskId, student1.Id);
        
        var student2 = await CreateAdditionalStudentAsync("otherstudent@example.com");
        SetBearerToken(student2.Token);

        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions?studentId={student1.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetSubmission_ReturnsNotFound_ForNonExistentSubmission()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/tasks/{taskId}/submissions/{nonExistentId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetSubmission_ReturnsNotFound_ForMismatchedTaskId()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        
        // Создаем первый курс, урок, задание и отправку
        var courseId1 = await CreateTestCourseAsync("Course 1");
        var lessonId1 = await CreateTestLessonAsync(courseId1, "Lesson 1");
        var taskId1 = await CreateTestTaskAsync(lessonId1, "Task 1");
        var student = await GetStudentAsync();
        var submissionId1 = await CreateTestSubmissionAsync(taskId1, student.Id);
        
        // Создаем второй курс, урок, задание и отправку
        var courseId2 = await CreateTestCourseAsync("Course 2");
        var lessonId2 = await CreateTestLessonAsync(courseId2, "Lesson 2");
        var taskId2 = await CreateTestTaskAsync(lessonId2, "Task 2");
        var submissionId2 = await CreateTestSubmissionAsync(taskId2, student.Id);

        // Пытаемся получить отправку с неправильным taskId (используем taskId1 для submissionId2)
        var response = await Client.GetAsync($"/api/tasks/{taskId1}/submissions/{submissionId2}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Submit_ValidatesRequiredFields()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);
        
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var invalidRequest = new
        {
            Code = "print('test')",
            BlocklyXml = (string?)null,
            BlockMap = (object?)null
        };

        var response = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions", invalidRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}