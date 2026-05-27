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

            // First create a draft
            var createDraftResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", new { });
            Assert.That(createDraftResponse.IsSuccessStatusCode, Is.True);
            var draft = await createDraftResponse.Content.ReadFromJsonAsync<SubmissionDto>();

            // Update the draft with code
            var updateRequest = new UpdateDraftRequest(code);
            var updateResponse = await Client.PutAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", updateRequest);
            Assert.That(updateResponse.IsSuccessStatusCode, Is.True);
            var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<SubmissionDto>();

            // Submit the draft
            var submitResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft/submit", new { });
            Assert.That(submitResponse.IsSuccessStatusCode, Is.True);

            // After submitting, we need to get the submission ID from the list of submissions
            // because SubmitDraft endpoint returns CheckResultDto, not SubmissionDto
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

        // Create draft
        var createResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", new { });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var draft = await createResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(draft, Is.Not.Null);
        Assert.That(draft!.TaskId, Is.EqualTo(taskId));
        Assert.That(draft!.StudentId, Is.EqualTo(student.Id.ToString()));
        Assert.That(draft!.IsDraft, Is.True);
        Assert.That(draft!.Code, Is.EqualTo(""));

        // Update draft
        var updateRequest = new UpdateDraftRequest("print('Test')");
        var updateResponse = await Client.PutAsJsonAsync($"/api/tasks/{taskId}/submissions/draft", updateRequest);
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedDraft = await updateResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(updatedDraft, Is.Not.Null);
        Assert.That(updatedDraft!.Code, Is.EqualTo("print('Test')"));
        Assert.That(updatedDraft!.IsDraft, Is.True);

        // Submit draft
        var submitResponse = await Client.PostAsJsonAsync($"/api/tasks/{taskId}/submissions/draft/submit", new { });
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // SubmitDraft endpoint returns SubmissionDto with CheckResultDto inside
        var submissionDto = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>();
        Assert.That(submissionDto, Is.Not.Null);
        Assert.That(submissionDto!.Result, Is.Not.Null);
        Assert.That(submissionDto!.Result!.FinalState, Is.Not.Null);
        Assert.That(submissionDto!.Result!.Issues, Is.Not.Null);

        // Fetch the actual submission from the list to verify it was created
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
    public async Task SubmitDraft_WithRawJson_AsFromFrontend_WorksCorrectly()
    {
        // Arrange: создаем задание
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);
        var taskId = await CreateTestTaskAsync(lessonId, "Task for Raw JSON Test");

        // Act: студент отправляет голые JSON'ы как с фронтенда
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик - отправляем пустой JSON объект
        var createDraftJson = "{}";
        var createContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с Python кодом - отправляем JSON с полями Code и BlocklyXml
        var updateDraftJson = @"{
            ""Code"": ""print('Hello from raw JSON!')"",
            ""BlocklyXml"": null
        }";
        var updateContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку - отправляем пустой JSON объект
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем ответ
        var responseJson = await submitResponse.Content.ReadAsStringAsync();
        Assert.That(responseJson, Is.Not.Null.Or.Empty);

        // Парсим JSON вручную для проверки структуры
        var jsonDoc = JsonDocument.Parse(responseJson);
        var root = jsonDoc.RootElement;

        // Проверяем основные поля SubmissionDto
        Assert.That(root.TryGetProperty("id", out _), Is.True, "Should have id field");
        Assert.That(root.TryGetProperty("taskId", out _), Is.True, "Should have taskId field");
        Assert.That(root.TryGetProperty("studentId", out _), Is.True, "Should have studentId field");
        Assert.That(root.TryGetProperty("code", out var codeProp), Is.True, "Should have code field");
        Assert.That(codeProp.GetString(), Is.EqualTo("print('Hello from raw JSON!')"));
        Assert.That(root.TryGetProperty("submittedAt", out _), Is.True, "Should have submittedAt field");
        Assert.That(root.TryGetProperty("isDraft", out var isDraftProp), Is.True, "Should have isDraft field");
        Assert.That(isDraftProp.GetBoolean(), Is.False, "Should not be a draft after submit");

        // Проверяем наличие результата проверки
        Assert.That(root.TryGetProperty("result", out var resultProp), Is.True, "Should have result field");
        Assert.That(resultProp.ValueKind, Is.EqualTo(JsonValueKind.Object));

        // Проверяем поля CheckResultDto внутри result
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
        // Arrange: создаем задачу с начальным и ожидаемым состоянием
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Создаем черновик задачи
        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        // Обновляем задачу с начальным состоянием и решением
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

        // Публикуем задачу
        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: студент создает черновик, обновляет его с правильным кодом и отправляет
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик отправки
        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с кодом, который перемещает кота на 10 пикселей
        var updateDraftJson = @"{
            ""Code"": ""move(10)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем результат
        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        // Отладочный вывод
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

        // Вывод финального состояния
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

        // Проверяем, что итоговое состояние содержит кота в правильной позиции (10, 0)
        var finalState2 = submission.Result.FinalState;
        Assert.That(finalState.Sprites, Has.Count.EqualTo(1));
        var catState = finalState.Sprites[0] as CatStateDto;
        Assert.That(catState, Is.Not.Null);
        Assert.That(catState!.X, Is.EqualTo(10.0), "Cat should be at X=10");
        Assert.That(catState.Y, Is.EqualTo(0.0), "Cat should be at Y=0");

        // Проверяем метрики
        Assert.That(submission.Result.Metrics, Contains.Key("StateScore"));
        Assert.That(submission.Result.Metrics["StateScore"], Is.EqualTo(1.0), "State score should be 1.0");
    }

    [Test]
    public async Task SubmitDraft_WithAppleCollection_ShouldPass()
    {
        // Arrange: создаем задачу с яблоком, которое нужно собрать
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Создаем черновик задачи
        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        // Обновляем задачу с начальным состоянием (кот и яблоко) и решением
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

        // Публикуем задачу
        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: студент создает черновик, обновляет его с правильным кодом и отправляет
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик отправки
        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с кодом, который перемещает кота на 50 пикселей (к яблоку)
        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем результат
        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        // Отладочный вывод
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

        // Проверяем, что итоговое состояние содержит кота в позиции (50, 0) и яблоко собрано (невидимо)
        var finalState = submission.Result.FinalState;
        // Яблоко остается в списке спрайтов, но становится невидимым
        Assert.That(finalState.Sprites, Has.Count.EqualTo(2), "Should have cat and apple (invisible)");
        var catState = finalState.Sprites.FirstOrDefault(s => s.Type == SpriteType.Cat) as CatStateDto;
        Assert.That(catState, Is.Not.Null, "Cat should be present");
        Assert.That(catState!.X, Is.EqualTo(50.0).Within(1.0), "Cat should be at X=50");
        Assert.That(catState.Y, Is.EqualTo(0.0).Within(1.0), "Cat should be at Y=0");
        // Проверяем, что яблоко невидимо
        var appleState = finalState.Sprites.FirstOrDefault(s => s.Type == SpriteType.Apple) as AppleStateDto;
        Assert.That(appleState, Is.Not.Null, "Apple should be present");
        Assert.That(appleState!.Visible, Is.False, "Apple should be invisible after collection");
        // Проверяем, что видимых спрайтов только один (кот)
        var visibleSprites = finalState.Sprites.Where(s => s.Visible).ToList();
        Assert.That(visibleSprites, Has.Count.EqualTo(1), "Only cat should be visible");
    }

    [Test]
    public async Task SubmitDraft_WithWallCollision_ShouldPass()
    {
        // Arrange: создаем задачу со стеной, которая не мешает движению (далеко)
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Создаем черновик задачи
        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        // Обновляем задачу с начальным состоянием (кот и стена) и решением (простое движение)
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

        // Публикуем задачу
        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: студент создает черновик, обновляет его с правильным кодом и отправляет
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик отправки
        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с кодом, который перемещает кота на 50 пикселей
        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем результат
        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        // Отладочный вывод
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

        // Проверяем, что итоговое состояние содержит кота в позиции (50, 0)
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
        // Arrange: создаем задачу со стеной на пути
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Создаем черновик задачи
        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        // Обновляем задачу с начальным состоянием (кот и стена) и решением (обход стены)
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

        // Публикуем задачу
        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: студент создает черновик, обновляет его с кодом, который сталкивается со стеной, и отправляет
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик отправки
        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с кодом, который двигает кота прямо в стену
        var updateDraftJson = @"{
            ""Code"": ""move(50)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем результат - должен быть неуспешным
        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");

        // Отладочный вывод
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

        // Ожидаем, что решение не прошло из-за столкновения со стеной
        Assert.That(submission.Result!.IsPassed, Is.False, "Task should fail due to wall collision");
        // Проверяем, что в ошибке есть упоминание стены (опционально)
        if (submission.Result.Issues != null && submission.Result.Issues.Count > 0)
        {
            var hasWallError = submission.Result.Issues.Any(i => i.Message.Contains("Кот столкнулся со стеной"));
            Assert.That(hasWallError, Is.True, "Should have wall collision error");
        }
    }

    [Test]
    public async Task SubmitDraft_ComplexScenario_ShouldPass()
    {
        // Arrange: создаем комплексную задачу с несколькими яблоками и стенами
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Создаем черновик задачи
        var createTaskRequest = new CreateTaskDraftRequest(
            lessonId,
            1);
        var createTaskResponse = await Client.PostAsJsonAsync("/api/tasks/draft", createTaskRequest);
        Assert.That(createTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var task = await createTaskResponse.Content.ReadFromJsonAsync<TaskDto>(JsonOptions);
        var taskId = task!.Id;

        // Обновляем задачу с комплексным начальным состоянием и решением
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
            ""solutionCode"": ""move(30)\nturn(90)\nmove(30)\nturn(-90)\nmove(30)"",
            ""config"": {
                ""sceneWidth"": 1000,
                ""sceneHeight"": 1000,
                ""tolerancePx"": 1.0
            }
        }";

        var updateContent = new StringContent(updateTaskJson, Encoding.UTF8, "application/json");
        var updateTaskResponse = await Client.PostAsync($"/api/tasks/{taskId}", updateContent);
        Assert.That(updateTaskResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Публикуем задачу
        var publishResponse = await Client.PostAsync($"/api/tasks/{taskId}/publish", null);
        Assert.That(publishResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: студент создает черновик, обновляет его с правильным кодом и отправляет
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // 1. Создаем черновик отправки
        var createDraftJson = "{}";
        var createDraftContent = new StringContent(createDraftJson, Encoding.UTF8, "application/json");
        var createDraftResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft", createDraftContent);
        Assert.That(createDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // 2. Обновляем черновик с кодом, который собирает яблоки и обходит стену
        var updateDraftJson = @"{
            ""Code"": ""move(30)\nturn(90)\nmove(30)\nturn(-90)\nmove(30)"",
            ""BlocklyXml"": null
        }";
        var updateDraftContent = new StringContent(updateDraftJson, Encoding.UTF8, "application/json");
        var updateDraftResponse = await Client.PutAsync($"/api/tasks/{taskId}/submissions/draft", updateDraftContent);
        Assert.That(updateDraftResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // 3. Отправляем черновик на проверку
        var submitJson = "{}";
        var submitContent = new StringContent(submitJson, Encoding.UTF8, "application/json");
        var submitResponse = await Client.PostAsync($"/api/tasks/{taskId}/submissions/draft/submit", submitContent);
        Assert.That(submitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Assert: проверяем результат
        var submission = await submitResponse.Content.ReadFromJsonAsync<SubmissionDto>(JsonOptions);
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission.IsDraft, Is.False, "Should not be a draft after submit");
        Assert.That(submission.Result, Is.Not.Null, "Should have check result");
        Assert.That(submission.Result!.IsPassed, Is.True, "Task should be passed");
        Assert.That(submission.Result.IsOptimal, Is.True, "Solution should be optimal");

        // Проверяем, что итоговое состояние содержит кота в конечной позиции и яблоки собраны
        var finalState = submission.Result.FinalState;
        // Должны остаться только кот и стена (яблоки собраны)
        var remainingSprites = finalState.Sprites.Count;
        // Ожидаем: кот + стена = 2 спрайта
        Assert.That(remainingSprites, Is.EqualTo(4), "Should have cat and wall (apples collected)");
        var catState = finalState.Sprites.OfType<CatStateDto>().FirstOrDefault();
        Assert.That(catState, Is.Not.Null);
        // Конечная позиция после движения: начальная (0,0) -> move(30) = (30,0) -> turn(90) направление вниз -> move(30) = (30,30) -> turn(-90) направление вправо -> move(30) = (60,30)
        Assert.That(catState!.X, Is.EqualTo(60.0).Within(1.0), "Cat should be at X=60");
        Assert.That(catState.Y, Is.EqualTo(30.0).Within(1.0), "Cat should be at Y=30");
    }
}