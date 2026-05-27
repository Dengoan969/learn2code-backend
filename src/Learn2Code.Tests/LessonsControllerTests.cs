using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class LessonsControllerTests : TestBase
{
    private async Task<Guid> CreateTestCourseAsync(string title = "Test Course for Lessons")
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

    private async Task<Guid> CreateTestLessonAsync(Guid courseId, string title = "Test Lesson", int order = 1)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createLessonRequest = new CreateLessonRequest(
                title,
                "Description",
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

    [Test]
    public async Task Teacher_CanGetLessonsByCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var response = await Client.GetAsync($"/api/lessons?courseId={courseId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var lessons = await response.Content.ReadFromJsonAsync<List<LessonDto>>();
        Assert.That(lessons, Is.Not.Null);
        Assert.That(lessons, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Teacher_CanGetLessonById()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var response = await Client.GetAsync($"/api/lessons/{lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
        Assert.That(lesson, Is.Not.Null);
        Assert.That(lesson!.Id, Is.EqualTo(lessonId));
        Assert.That(lesson!.Title, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Teacher_CanCreateLesson()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();

        var request = new CreateLessonRequest(
            "New Lesson",
            "New Description",
            2,
            courseId);

        var response = await Client.PostAsJsonAsync("/api/lessons", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
        Assert.That(lesson, Is.Not.Null);
        Assert.That(lesson!.Title, Is.EqualTo("New Lesson"));
        Assert.That(lesson!.CourseId, Is.EqualTo(courseId));
    }

    [Test]
    public async Task Teacher_CanUpdateOwnLesson()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        var updateRequest = new UpdateLessonRequest(
            "Updated Title",
            "Updated Description",
            3,
            null); // CourseId can be null when updating

        var response = await Client.PutAsJsonAsync($"/api/lessons/{lessonId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/lessons/{lessonId}");
        var lesson = await getResponse.Content.ReadFromJsonAsync<LessonDto>();
        Assert.That(lesson!.Title, Is.EqualTo("Updated Title"));
        Assert.That(lesson.Description, Is.EqualTo("Updated Description"));
        Assert.That(lesson.Order, Is.EqualTo(3));
    }

    [Test]
    public async Task Teacher_CanDeleteOwnLesson()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();

        var createRequest = new CreateLessonRequest(
            "Lesson to Delete",
            "Description",
            99,
            courseId);
        var createResponse = await Client.PostAsJsonAsync("/api/lessons", createRequest);
        var lesson = await createResponse.Content.ReadFromJsonAsync<LessonDto>();
        var lessonId = lesson!.Id;

        var deleteResponse = await Client.DeleteAsync($"/api/lessons/{lessonId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/lessons/{lessonId}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Teacher_CannotGetLessonsOfOtherTeacherCourse()
    {
        // Create course with first teacher
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();

        // Try to access with second teacher
        var otherTeacher = await CreateAdditionalTeacherAsync("another@example.com");
        SetBearerToken(otherTeacher.Token);

        var response = await Client.GetAsync($"/api/lessons?courseId={courseId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CannotGetLessonOfOtherTeacher()
    {
        // Create course and lesson with first teacher
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Try to access with second teacher
        var otherTeacher = await CreateAdditionalTeacherAsync("another2@example.com");
        SetBearerToken(otherTeacher.Token);

        var response = await Client.GetAsync($"/api/lessons/{lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CannotUpdateLessonOfOtherTeacher()
    {
        // Create course and lesson with first teacher
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Try to update with second teacher
        var otherTeacher = await CreateAdditionalTeacherAsync("another3@example.com");
        SetBearerToken(otherTeacher.Token);

        var updateRequest = new UpdateLessonRequest("Hacked Title", null, null, null);
        var response = await Client.PutAsJsonAsync($"/api/lessons/{lessonId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CannotDeleteLessonOfOtherTeacher()
    {
        // Create course and lesson with first teacher
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Try to delete with second teacher
        var otherTeacher = await CreateAdditionalTeacherAsync("another4@example.com");
        SetBearerToken(otherTeacher.Token);

        var response = await Client.DeleteAsync($"/api/lessons/{lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanGetAnyLesson()
    {
        // Create course and lesson with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Admin can access
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/lessons/{lessonId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Admin_CanUpdateAnyLesson()
    {
        // Create course and lesson with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var lessonId = await CreateTestLessonAsync(courseId);

        // Admin can update
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var updateRequest = new UpdateLessonRequest("Admin Updated", null, null, null);
        var response = await Client.PutAsJsonAsync($"/api/lessons/{lessonId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Admin_CanDeleteAnyLesson()
    {
        // Create course with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        // Admin creates and deletes lesson
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var createRequest = new CreateLessonRequest(
            "Lesson for Admin Delete",
            "Desc",
            100,
            courseId);
        var createResponse = await Client.PostAsJsonAsync("/api/lessons", createRequest);
        var lesson = await createResponse.Content.ReadFromJsonAsync<LessonDto>();
        var lessonId = lesson!.Id;

        var deleteResponse = await Client.DeleteAsync($"/api/lessons/{lessonId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Student_CannotAccessLessons_WhenNotEnrolled()
    {
        // Create course with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        // Student tries to access (not enrolled)
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var response = await Client.GetAsync($"/api/lessons?courseId={courseId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CanAccessLessons_WhenEnrolled()
    {
        // Create course with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        // Create a lesson in the course
        var lessonId = await CreateTestLessonAsync(courseId);

        // Create a group for the course
        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        // Create a student and add to group
        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Now test that student can access lessons
        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/lessons?courseId={courseId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var lessons = await response.Content.ReadFromJsonAsync<List<LessonDto>>();
        Assert.That(lessons, Is.Not.Null);
        Assert.That(lessons!.Count, Is.EqualTo(1));
        Assert.That(lessons[0].Id, Is.EqualTo(lessonId));
    }

    [Test]
    public async Task Student_CanGetLessonById_WhenEnrolled()
    {
        // Create course with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        // Create a lesson in the course
        var lessonId = await CreateTestLessonAsync(courseId);

        // Create a group for the course
        var groupRequest = new { courseId, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);

        // Create a student and add to group
        var student = await GetStudentAsync();
        var addStudentResponse =
            await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Now test that student can get lesson by ID
        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/lessons/{lessonId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
        Assert.That(lesson, Is.Not.Null);
        Assert.That(lesson!.Id, Is.EqualTo(lessonId));
    }

    [Test]
    public async Task Student_CannotGetLessonById_WhenNotEnrolled()
    {
        // Create course with teacher
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        // Create a lesson in the course
        var lessonId = await CreateTestLessonAsync(courseId);

        // Create a student (not enrolled in any group)
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        // Student should not be able to access the lesson
        var response = await Client.GetAsync($"/api/lessons/{lessonId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetLessons_ReturnsBadRequest_WhenCourseIdMissing()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var response = await Client.GetAsync("/api/lessons");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetLesson_ReturnsNotFound_ForNonExistentId()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/lessons/{nonExistentId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateLesson_ValidatesRequiredFields()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();

        var invalidRequest = new
        {
            Description = "Desc",
            Order = 1,
            CourseId = courseId
        };

        var response = await Client.PostAsJsonAsync("/api/lessons", invalidRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}