using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Enums;

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

    private async Task<Guid> CreateTestLessonAsync(Guid courseId, string title = "Test Lesson for Submissions",
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

    private async Task<Guid> CreateTestSubmissionAsync(Guid taskId, Guid studentId,
        string code = "print('Hello, World!')")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var student = await GetStudentAsync();
            SetBearerToken(student.Token);

            var createDraftResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", new { });
            Assert.That(createDraftResponse.IsSuccessStatusCode, Is.True);
            var draft = await createDraftResponse.Content.ReadFromJsonAsync<SubmissionDto>();

            var updateRequest = new UpdateDraftRequest(code);
            var updateResponse = await Client.PutAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", updateRequest);
            Assert.That(updateResponse.IsSuccessStatusCode, Is.True);
            var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<SubmissionDto>();

            var submitResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft/submit", new { });
            Assert.That(submitResponse.IsSuccessStatusCode, Is.True);

            var listResponse = await Client.GetAsync($"/api/tasks/{taskId}/submissions");
            Assert.That(listResponse.IsSuccessStatusCode, Is.True);
            var submissions = await listResponse.Content.ReadFromJsonAsync<List<SubmissionDto>>();
            var submission = submissions!.FirstOrDefault(s => s.StudentId == student.Id.ToString() && !s.IsDraft);
            Assert.That(submission, Is.Not.Null, "Submitted submission not found in list");
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
    public async Task Student_CanCreateAndSubmitDraft()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", new { });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var draft = await createResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(draft, Is.Not.Null);
        Assert.That(draft!.TaskId, Is.EqualTo(taskId));
        Assert.That(draft!.StudentId, Is.EqualTo(student.Id.ToString()));
        Assert.That(draft!.IsDraft, Is.True);
        Assert.That(draft!.Code, Is.EqualTo(""));

        var updateRequest = new UpdateDraftRequest("print('Test')");
        var updateResponse = await Client.PutAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", updateRequest);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(updatedDraft, Is.Not.Null);
        Assert.That(updatedDraft!.Code, Is.EqualTo("print('Test')"));
        Assert.That(updatedDraft!.IsDraft, Is.True);

        var submitResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft/submit", new { });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submissionDto = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submissionDto, Is.Not.Null);
        Assert.That(submissionDto!.Result, Is.Not.Null);
        Assert.That(submissionDto!.Result!.FinalState, Is.Not.Null);
        Assert.That(submissionDto!.Result!.Issues, Is.Not.Null);

        var listResponse = await Client.GetAsync($"/api/tasks/{taskId}/submissions");
        Assert.That(listResponse.IsSuccessStatusCode, Is.True);
        var submissions = await listResponse.Content.ReadFromJsonAsync<List<SubmissionDto>>();
        var submission = submissions!.FirstOrDefault(s => s.StudentId == student.Id.ToString() && !s.IsDraft);
        Assert.That(submission, Is.Not.Null, "Submitted submission not found in list");
        Assert.That(submission!.TaskId, Is.EqualTo(taskId));
        Assert.That(submission!.StudentId, Is.EqualTo(student.Id.ToString()));
        Assert.That(submission!.Code, Is.EqualTo("print('Test')"));
        Assert.That(submission!.IsDraft, Is.False);
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

        var courseId1 = await CreateTestCourseAsync("Course 1");
        var lessonId1 = await CreateTestLessonAsync(courseId1, "Lesson 1");
        var taskId1 = await CreateTestTaskAsync(lessonId1, "Task 1");
        var student = await GetStudentAsync();
        var submissionId1 = await CreateTestSubmissionAsync(taskId1, student.Id);

        var courseId2 = await CreateTestCourseAsync("Course 2");
        var lessonId2 = await CreateTestLessonAsync(courseId2, "Lesson 2");
        var taskId2 = await CreateTestTaskAsync(lessonId2, "Task 2");
        var submissionId2 = await CreateTestSubmissionAsync(taskId2, student.Id);

        var response = await Client.GetAsync($"/api/tasks/{taskId1}/submissions/{submissionId2}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }


    [Test]
    public async Task SubmitDraft_WithRawJson_AsFromFrontend_WorksCorrectly()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId, "Task for Raw JSON Test");

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""print('Hello from raw JSON!')"",
            ""BlocklyXml"": null
        }";
        var updateContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var responseJson = await submitResponse.Content.ReadAsStringAsync();
        Assert.That(responseJson, Is.Not.Null.Or.Empty);

        var jsonDoc = JsonDocument.Parse(responseJson);
        var root = jsonDoc.RootElement;

        Assert.That(root.TryGetProperty("id", out _), Is.True, "Should have id field");
        Assert.That(root.TryGetProperty("taskId", out _), Is.True, "Should have taskId field");
        Assert.That(root.TryGetProperty("studentId", out _), Is.True, "Should have studentId field");
        Assert.That(root.TryGetProperty("code", out var codeProp), Is.True, "Should have code field");
        Assert.That(codeProp.GetString(), Is.EqualTo("print('Hello from raw JSON!')"));
        Assert.That(root.TryGetProperty("submittedAt", out _), Is.True, "Should have submittedAt field");
        Assert.That(root.TryGetProperty("isDraft", out var isDraftProp), Is.True, "Should have isDraft field");
        Assert.That(isDraftProp.GetBoolean(), Is.False, "Should not be a draft after submit");

        Assert.That(root.TryGetProperty("result", out var resultProp), Is.True, "Should have result field");
        Assert.That(resultProp.ValueKind, Is.EqualTo(JsonValueKind.Object));

        Assert.That(resultProp.TryGetProperty("isPassed", out _), Is.True, "Result should have isPassed field");
        Assert.That(resultProp.TryGetProperty("isOptimal", out _), Is.True, "Result should have isOptimal field");
        Assert.That(resultProp.TryGetProperty("hint", out _), Is.True, "Result should have hint field");
        Assert.That(resultProp.TryGetProperty("issues", out _), Is.True, "Result should have issues field");
        Assert.That(resultProp.TryGetProperty("metrics", out _), Is.True, "Result should have metrics field");
        Assert.That(resultProp.TryGetProperty("finalState", out _), Is.True, "Result should have finalState field");
    }

    [Test]
    public async Task SubmitDraft_WithRealTaskExecution_ValidatesStateChange()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Move Cat Task"",
            ""description"": ""Move cat 10 pixels to the right"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(10)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";

        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""move(10)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        Console.WriteLine($"Result: IsPassed={submission.Result!.IsPassed}, IsOptimal={submission.Result.IsOptimal}");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            Console.WriteLine($"Issues count: {submission.Result.Issues.Count}");
            foreach (var issue in submission.Result.Issues) Console.WriteLine($"  - {issue.Type}: {issue.Message}");
        }
        else
        {
            Console.WriteLine("No issues");
        }

        var finalState = submission.Result.FinalState;
        Console.WriteLine($"FinalState sprites count: {finalState.Sprites.Count}");
        foreach (var sprite in finalState.Sprites)
        {
            Console.WriteLine($"  - {sprite.Type}: X={sprite.X}, Y={sprite.Y}, Visible={sprite.Visible}");
            if (sprite is CatStateDto cat)
                Console.WriteLine($"    Direction={cat.Direction}, CollectedItems={cat.CollectedItems?.Count ?? 0}");
        }

        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");
        Assert.That(submission.Result.FinalState, Is.Not.Null, "Should have final state");

        var finalState2 = submission.Result.FinalState;
        Assert.That(finalState.Sprites, Has.Count.EqualTo(1));
        var catState = finalState.Sprites[0] as CatStateDto;
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.X, Is.EqualTo(10.0), "Cat should be at X=10");
        Assert.That(catState.Y, Is.EqualTo(0.0), "Cat should be at Y=0");

        Assert.That(submission.Result.Metrics, Contains.Key("StateScore"));
        Assert.That(submission.Result.Metrics["StateScore"], Is.EqualTo(1.0), "State score should be 1.0");
    }

    [Test]
    public async Task SubmitDraft_WithAppleCollection_ShouldPass()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Collect Apple Task"",
            ""description"": ""Move cat to collect apple"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""apple"",
                        ""x"": 50.0,
                        ""y"": 0.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(50)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";

        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        Console.WriteLine($"Result: IsPassed={submission.Result!.IsPassed}, IsOptimal={submission.Result.IsOptimal}");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            Console.WriteLine($"Issues count: {submission.Result.Issues.Count}");
            foreach (var issue in submission.Result.Issues) Console.WriteLine($"  - {issue.Type}: {issue.Message}");
        }
        else
        {
            Console.WriteLine("No issues");
        }

        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");

        var finalState = submission.Result.FinalState;
        Assert.That(finalState.Sprites, Has.Count.EqualTo(2), "Should have cat and apple (invisible)");
        var catState = finalState.Sprites.FirstOrDefault(s => s.Type == SpriteType.Cat) as CatStateDto;
        Assert.That(catState, Is.Not.Null, "Cat should be present");
        Assert.That(catState!.X, Is.EqualTo(50.0).Within(1.0), "Cat should be at X=50");
        Assert.That(catState.Y, Is.EqualTo(0.0).Within(1.0), "Cat should be at Y=0");
        var appleState = finalState.Sprites.FirstOrDefault(s => s.Type == SpriteType.Apple) as AppleStateDto;
        Assert.That(appleState, Is.Not.Null, "Apple should be present");
        Assert.That(appleState!.Visible, Is.False, "Apple should be invisible after collection");
        var visibleSprites = finalState.Sprites.Where(s => s.Visible).ToList();
        Assert.That(visibleSprites, Has.Count.EqualTo(1), "Only cat should be visible");
    }

    [Test]
    public async Task SubmitDraft_WithWallCollision_ShouldPass()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Wall Far Away Task"",
            ""description"": ""Move cat while wall is far away"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""wall"",
                        ""x"": 200.0,
                        ""y"": 0.0,
                        ""width"": 20.0,
                        ""height"": 40.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(50)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";

        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        Console.WriteLine($"Result: IsPassed={submission.Result!.IsPassed}, IsOptimal={submission.Result.IsOptimal}");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            Console.WriteLine($"Issues count: {submission.Result.Issues.Count}");
            foreach (var issue in submission.Result.Issues) Console.WriteLine($"  - {issue.Type}: {issue.Message}");
        }
        else
        {
            Console.WriteLine("No issues");
        }

        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");

        var finalState = submission.Result.FinalState;
        Assert.That(finalState.Sprites, Has.Count.EqualTo(2), "Should have cat and wall");
        var catState = finalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.X, Is.EqualTo(50.0).Within(1.0), "Cat should be at X=50");
        Assert.That(catState.Y, Is.EqualTo(0.0).Within(1.0), "Cat should be at Y=0");
    }

    [Test]
    public async Task SubmitDraft_WithWallCollision_ShouldFail()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Wall Collision Task"",
            ""description"": ""Move cat, collision should fail"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""wall"",
                        ""x"": 60.0,
                        ""y"": 0.0,
                        ""width"": 20.0,
                        ""height"": 40.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(20)\nturn(-90)\nmove(45)\nturn(90)\nmove(60)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";

        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        Console.WriteLine($"Result: IsPassed={submission.Result!.IsPassed}, IsOptimal={submission.Result.IsOptimal}");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            Console.WriteLine($"Issues count: {submission.Result.Issues.Count}");
            foreach (var issue in submission.Result.Issues) Console.WriteLine($"  - {issue.Type}: {issue.Message}");
        }
        else
        {
            Console.WriteLine("No issues");
        }

        Assert.That(submission.Result!.IsPassed, Is.False, "Task should fail due to wall collision");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            var hasWallError = submission.Result.Issues.Any(i => i.Message.Contains("Кот столкнулся со стеной"));
            Assert.That(hasWallError, Is.True, "Should have wall collision error");
        }
    }

    [Test]
    public async Task SubmitDraft_ComplexScenario_ShouldPass()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Complex Collection Task"",
            ""description"": ""Collect apples while avoiding walls"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""apple"",
                        ""x"": 30.0,
                        ""y"": 0.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""apple"",
                        ""x"": 60.0,
                        ""y"": 30.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    },
                    {
                        ""type"": ""wall"",
                        ""x"": 40.0,
                        ""y"": -50.0,
                        ""width"": 10.0,
                        ""height"": 40.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(30)\nturn(90)\nmove(30)\nturn(-90)\nmove(30)\nsay(\""Hello\"")"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";
        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""move(30)\nturn(90)\nmove(30)\nturn(-90)\nmove(30)\nsay(\""Hello\"")"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");
        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");

        var finalState = submission.Result.FinalState;
        var remainingSprites = finalState.Sprites.Count;
        Assert.That(remainingSprites, Is.EqualTo(4), "Should have cat and wall (apples collected)");
        var catState = finalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.X, Is.EqualTo(60.0).Within(1.0), "Cat should be at X=60");
        Assert.That(catState.Y, Is.EqualTo(30.0).Within(1.0), "Cat should be at Y=30");
        Assert.That(catState.SaidTexts, Is.Not.Null, "Cat should have saidTexts");
        Assert.That(catState.SaidTexts, Contains.Key("Hello"), "Cat should have said 'Hello'");
        Assert.That(catState.SaidTexts["Hello"], Is.EqualTo(1), "Cat should have said 'Hello' exactly once");
    }

    [Test]
    public async Task SubmitDraft_WithSayCommand_ShouldPass()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Say Hello Task"",
            ""description"": ""Make the cat say Hello"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""say(\""Hello\"")"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";
        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""say(\""Hello\"")"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        Console.WriteLine($"Result: IsPassed={submission.Result!.IsPassed}, IsOptimal={submission.Result.IsOptimal}");
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            Console.WriteLine($"Issues count: {submission.Result.Issues.Count}");
            foreach (var issue in submission.Result.Issues) Console.WriteLine($"  - {issue.Type}: {issue.Message}");
        }
        else
        {
            Console.WriteLine("No issues");
        }

        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");

        var finalState = submission.Result.FinalState;
        Assert.That(finalState.Sprites, Has.Count.EqualTo(1), "Should have only cat");
        var catState = finalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null, "Cat should be present");
        Assert.That(catState!.SaidTexts, Is.Not.Null, "Cat should have saidTexts");
        Assert.That(catState.SaidTexts, Contains.Key("Hello"), "Cat should have said 'Hello'");
        Assert.That(catState.SaidTexts["Hello"], Is.EqualTo(1), "Cat should have said 'Hello' exactly once");
        Assert.That(catState.X, Is.EqualTo(0.0).Within(1.0), "Cat should stay at X=0");
        Assert.That(catState.Y, Is.EqualTo(0.0).Within(1.0), "Cat should stay at Y=0");
    }

    [Test]
    public async Task SubmitDraft_WithWrongSayText_ShouldFail()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(lessonId, 1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Say Hello Task"",
            ""description"": ""Make the cat say Hello"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""say(\""Hello\"")"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";
        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""say(\""Hi\"")"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False);
        Assert.That(submission.Result, Is.Not.Null);
        Assert.That(submission.Result!.IsPassed, Is.False, "Task should fail because said wrong text");
        Assert.That(submission.Result.IsOptimal, Is.False, "Should not be optimal");

        var catState = submission.Result.FinalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.SaidTexts, Is.Not.Null);
        Assert.That(catState.SaidTexts, Contains.Key("Hi"), "Cat should have said 'Hi'");
        Assert.That(catState.SaidTexts.ContainsKey("Hello"), Is.False, "Cat should NOT have said 'Hello'");
    }

    [Test]
    public async Task SubmitDraft_WithSayAndRedundantMove_ShouldBeSuboptimal()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(lessonId, 1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Say Only Task"",
            ""description"": ""Just say Hello, no moves"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""say(\""Hello\"")"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";
        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""say(\""Hello\"")\nmove(0)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False);
        Assert.That(submission.Result, Is.Not.Null);
        Assert.That(submission.Result!.IsPassed, Is.True, "Task should pass — state matches");
        Assert.That(submission.Result.IsOptimal, Is.False, "Should NOT be optimal — redundant move(0)");

        var catState = submission.Result.FinalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.SaidTexts, Is.Not.Null);
        Assert.That(catState.SaidTexts, Contains.Key("Hello"), "Cat should have said 'Hello'");
        Assert.That(catState.SaidTexts["Hello"], Is.EqualTo(1));
    }

    [Test]
    public async Task SubmitDraft_WithSayInWrongOrder_ShouldBeSuboptimal()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var createTaskRequest = new CreateTaskDraftRequest(lessonId, 1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        var updateTaskJson = @"{
            ""title"": ""Move-Say-Move Task"",
            ""description"": ""Move, say Hello, move again"",
            ""order"": 1,
            ""initialState"": {
                ""sprites"": [
                    {
                        ""type"": ""cat"",
                        ""x"": 0.0,
                        ""y"": 0.0,
                        ""direction"": 90.0,
                        ""visible"": true,
                        ""costume"": ""default""
                    }
                ]
            },
            ""solutionCode"": ""move(30)\nsay(\""Hello\"")\nmove(30)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";
        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var updateDraftJson = @"{
            ""Code"": ""say(\""Hello\"")\nmove(30)\nmove(30)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False);
        Assert.That(submission.Result, Is.Not.Null);
        Assert.That(submission.Result!.IsPassed, Is.False, "Should NOT pass — say was at wrong position");
        Assert.That(submission.Result.IsOptimal, Is.False, "Should NOT be optimal — wrong order of operations");

        var catState = submission.Result.FinalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.X, Is.EqualTo(60.0).Within(1.0), "Cat should be at X=60");
        Assert.That(catState.Y, Is.EqualTo(0.0).Within(1.0), "Cat should be at Y=0");
        Assert.That(catState.SaidTexts, Is.Not.Null);
        Assert.That(catState.SaidTexts, Contains.Key("Hello"), "Cat should have said 'Hello'");
        Assert.That(catState.SaidTexts["Hello"], Is.EqualTo(1));

        Assert.That(submission.Result.Hint, Does.Contain("не в том месте"), "Hint should mention wrong say position");
    }
}