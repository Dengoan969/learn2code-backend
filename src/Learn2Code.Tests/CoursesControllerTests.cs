using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Learn2Code.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class CoursesControllerTests : TestBase
{
    [Test]
    public async Task Admin_CanGetAllCourses()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync("/api/courses");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var courses = await response.Content.ReadFromJsonAsync<List<CourseDto>>();
        Assert.That(courses, Is.Not.Null);
    }

    [Test]
    public async Task Teacher_CanGetOwnCourses()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Test Course", "Test Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(createdCourse, Is.Not.Null);

        var response = await Client.GetAsync("/api/courses");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var courses = await response.Content.ReadFromJsonAsync<List<CourseDto>>();
        Assert.That(courses, Is.Not.Null);

        var ourCourses = courses!.Where(c => c.TeacherId == teacher.Id).ToList();
        Assert.That(ourCourses, Has.Count.AtLeast(1), "Учитель должен иметь хотя бы один курс");

        var foundCourse = ourCourses.FirstOrDefault(c => c.Id == createdCourse!.Id);
        Assert.That(foundCourse, Is.Not.Null, "Созданный курс должен быть в списке учителя");
        Assert.That(foundCourse!.Title, Is.EqualTo("Test Course"));
        Assert.That(foundCourse.TeacherId, Is.EqualTo(teacher.Id));
    }

    [Test]
    public async Task Student_CanGetCourses_WhenNotEnrolled_ReturnsEmptyList()
    {
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var response = await Client.GetAsync("/api/courses");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var courses = await response.Content.ReadFromJsonAsync<List<CourseDto>>();
        Assert.That(courses, Is.Not.Null);
        Assert.That(courses!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Student_CanGetCourses_WhenEnrolled_ReturnsCourses()
    {
        // Create a teacher and course
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        
        var createRequest = new CreateCourseRequest("Test Course", "Course Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var course = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        
        // Create a group for the course
        var groupRequest = new { courseId = course!.Id, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        
        // Create a student and add to group
        var student = await GetStudentAsync();
        var addStudentResponse = await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Now test that student can see the course
        SetBearerToken(student.Token);
        var getResponse = await Client.GetAsync("/api/courses");
        
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var courses = await getResponse.Content.ReadFromJsonAsync<List<CourseDto>>();
        Assert.That(courses, Is.Not.Null);
        Assert.That(courses!.Count, Is.EqualTo(1));
        Assert.That(courses[0].Id, Is.EqualTo(course.Id));
        Assert.That(courses[0].Title, Is.EqualTo("Test Course"));
    }

    [Test]
    public async Task Student_CanGetCourseById_WhenEnrolled()
    {
        // Create a teacher and course
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        
        var createRequest = new CreateCourseRequest("Test Course", "Course Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var course = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        
        // Create a group for the course
        var groupRequest = new { courseId = course!.Id, name = "Test Group" };
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", groupRequest);
        Assert.That(groupResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        
        // Create a student and add to group
        var student = await GetStudentAsync();
        var addStudentResponse = await Client.PostAsJsonAsync($"/api/groups/{group!.Id}/students", new { studentId = student.Id });
        Assert.That(addStudentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Now test that student can get the course by ID
        SetBearerToken(student.Token);
        var getResponse = await Client.GetAsync($"/api/courses/{course.Id}");
        
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var retrievedCourse = await getResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(retrievedCourse, Is.Not.Null);
        Assert.That(retrievedCourse!.Id, Is.EqualTo(course.Id));
        Assert.That(retrievedCourse.Title, Is.EqualTo("Test Course"));
    }

    [Test]
    public async Task Student_CannotGetCourseById_WhenNotEnrolled()
    {
        // Create a teacher and course
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        
        var createRequest = new CreateCourseRequest("Test Course", "Course Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var course = await createResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        
        // Create a student (not enrolled in any group)
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);
        
        // Student should not be able to access the course
        var getResponse = await Client.GetAsync($"/api/courses/{course!.Id}");
        
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanCreateCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("New Course", "Course Description");

        var response = await Client.PostAsJsonAsync("/api/courses", createRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var course = await response.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        Assert.That(course!.Title, Is.EqualTo("New Course"));
        Assert.That(course.Description, Is.EqualTo("Course Description"));
        Assert.That(course.TeacherId, Is.EqualTo(teacher.Id));
    }

    [Test]
    public async Task Admin_CanCreateCourse()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);
        
        // Debug: verify admin user exists in database before creating course
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbAdmin = await dbContext.Users.FindAsync(admin.Id);
            Console.WriteLine($"DEBUG: Before course creation - Admin exists: {dbAdmin != null}, ID: {admin.Id}");
            if (dbAdmin == null)
            {
                Console.WriteLine("DEBUG: Admin user not found! Listing all users:");
                var allUsers = await dbContext.Users.ToListAsync();
                foreach (var user in allUsers)
                {
                    Console.WriteLine($"  - {user.Login} ({user.Id})");
                }
            }
            else
            {
                Console.WriteLine($"DEBUG: Admin user details - Login: {dbAdmin.Login}, Role: {dbAdmin.Role}");
            }
        }

        // Also verify the token is valid by calling /api/auth/me
        var meResponse = await Client.GetAsync("/api/auth/me");
        if (meResponse.IsSuccessStatusCode)
        {
            var meContent = await meResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"DEBUG: /api/auth/me response: {meContent}");
        }
        else
        {
            Console.WriteLine($"DEBUG: /api/auth/me failed: {meResponse.StatusCode}");
        }

        var createRequest = new CreateCourseRequest("Admin Course", "Created by admin");

        var response = await Client.PostAsJsonAsync("/api/courses", createRequest);

        // Debug: print response content if not successful
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"DEBUG: Response status: {response.StatusCode}");
            Console.WriteLine($"DEBUG: Response content: {content}");
            
            // Try to get more details about the error
            try
            {
                var errorObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                if (errorObj != null && errorObj.ContainsKey("message"))
                {
                    Console.WriteLine($"DEBUG: Error message: {errorObj["message"]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Failed to parse error content: {ex.Message}");
            }
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var course = await response.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        Assert.That(course!.Title, Is.EqualTo("Admin Course"));
        Assert.That(course.TeacherId, Is.EqualTo(admin.Id));
    }

    [Test]
    public async Task Student_CannotCreateCourse()
    {
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var createRequest = new CreateCourseRequest("Student Course", "Should fail");

        var response = await Client.PostAsJsonAsync("/api/courses", createRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanGetCourseById()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("My Course", "My Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var response = await Client.GetAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var course = await response.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        Assert.That(course!.Id, Is.EqualTo(createdCourse.Id));
        Assert.That(course.Title, Is.EqualTo("My Course"));
        Assert.That(course.TeacherId, Is.EqualTo(teacher.Id));
    }

    [Test]
    public async Task Teacher_CannotGetOtherTeacherCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);

        var createRequest = new CreateCourseRequest("Teacher1 Course", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var teacher2 = await CreateAdditionalTeacherAsync();
        SetBearerToken(teacher2.Token);

        var response = await Client.GetAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanGetAnyCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Teacher Course", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var course = await response.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(course, Is.Not.Null);
        Assert.That(course!.Id, Is.EqualTo(createdCourse.Id));
    }

    [Test]
    public async Task Teacher_CanUpdateOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Original Title", "Original Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var updateRequest = new UpdateCourseRequest("Updated Title", "Updated Description");

        var response = await Client.PutAsJsonAsync($"/api/courses/{createdCourse!.Id}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/courses/{createdCourse.Id}");
        var updatedCourse = await getResponse.Content.ReadFromJsonAsync<CourseDto>();
        Assert.That(updatedCourse!.Title, Is.EqualTo("Updated Title"));
        Assert.That(updatedCourse.Description, Is.EqualTo("Updated Description"));
        Assert.That(updatedCourse.TeacherId, Is.EqualTo(teacher.Id));
    }

    [Test]
    public async Task Teacher_CannotUpdateOtherTeacherCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);

        var createRequest = new CreateCourseRequest("Teacher1 Course", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var teacher2 = await CreateAdditionalTeacherAsync();
        SetBearerToken(teacher2.Token);

        var updateRequest = new UpdateCourseRequest("Hacked Title", "Hacked Description");

        var response = await Client.PutAsJsonAsync($"/api/courses/{createdCourse!.Id}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanUpdateAnyCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Original Title", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var updateRequest = new UpdateCourseRequest("Admin Updated Title", "Admin Updated Description");

        var response = await Client.PutAsJsonAsync($"/api/courses/{createdCourse!.Id}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Teacher_CanDeleteOwnCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Course to Delete", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var response = await Client.DeleteAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/courses/{createdCourse.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Teacher_CannotDeleteOtherTeacherCourse()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);

        var createRequest = new CreateCourseRequest("Teacher1 Course", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var teacher2 = await CreateAdditionalTeacherAsync();
        SetBearerToken(teacher2.Token);

        var response = await Client.DeleteAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanDeleteAnyCourse()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new CreateCourseRequest("Course to Delete", "Description");
        var createResponse = await Client.PostAsJsonAsync("/api/courses", createRequest);
        var createdCourse = await createResponse.Content.ReadFromJsonAsync<CourseDto>();

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.DeleteAsync($"/api/courses/{createdCourse!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task GetCourse_ReturnsNotFound_ForNonExistentId()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/api/courses/{nonExistentId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateCourse_ValidatesRequiredFields()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createRequest = new { };

        var response = await Client.PostAsJsonAsync("/api/courses", createRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}