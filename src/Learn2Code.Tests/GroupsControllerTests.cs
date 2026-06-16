using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class GroupsControllerTests : TestBase
{
    private async Task<Guid> CreateTestCourseAsync(string title = "Test Course for Groups")
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
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Assert.Fail(
                    $"Failed to create course. Status: {response.StatusCode}, Content: {errorContent}. TeacherId: {teacher.Id}");
            }

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

    private async Task<Guid> CreateTestGroupAsync(Guid courseId, Guid teacherId, string name = "Test Group")
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var createGroupRequest = new CreateGroupRequest(
                name,
                "Group Description",
                courseId,
                teacherId);
            var response = await Client.PostAsJsonAsync("/api/groups", createGroupRequest);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Assert.Fail(
                    $"Failed to create group. Status: {response.StatusCode}, Content: {errorContent}. CourseId: {courseId}, TeacherId: {teacherId}, CurrentTeacherId: {teacher.Id}");
            }

            var group = await response.Content.ReadFromJsonAsync<GroupDto>();
            return group!.Id;
        }
        finally
        {
            if (oldAuth != null)
                Client.DefaultRequestHeaders.Authorization = oldAuth;
            else
                Client.DefaultRequestHeaders.Remove("Authorization");
        }
    }

    private async Task AddStudentToGroupAsync(Guid groupId, Guid studentId)
    {
        var oldAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            var teacher = await GetTeacherAsync();
            SetBearerToken(teacher.Token);

            var addStudentRequest = new AddStudentToGroupRequest(studentId);
            var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", addStudentRequest);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.Fail(
                    $"Failed to add student to group. Status: {response.StatusCode}, Content: {content}. GroupId: {groupId}, StudentId: {studentId}");
            }
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
    public async Task Teacher_CanCreateGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();

        var request = new CreateGroupRequest(
            "New Group",
            "New Description",
            courseId,
            teacher.Id);
        var response = await Client.PostAsJsonAsync("/api/groups", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var group = await response.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.Name, Is.EqualTo("New Group"));
        Assert.That(group!.TeacherId, Is.EqualTo(teacher.Id));
    }

    [Test]
    public async Task Admin_CanCreateGroup()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var teacher = await GetTeacherAsync();

        var request = new CreateGroupRequest(
            "Admin Group",
            "Admin Description",
            courseId,
            teacher.Id);
        var response = await Client.PostAsJsonAsync("/api/groups", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Student_CannotCreateGroup()
    {
        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var courseId = await CreateTestCourseAsync();
        var teacher = await GetTeacherAsync();

        var request = new CreateGroupRequest(
            "Student Group",
            "Should fail",
            courseId,
            teacher.Id);
        var response = await Client.PostAsJsonAsync("/api/groups", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanViewOwnGroups()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var createCourseRequest = new CreateCourseRequest(
            "Test Course for Groups",
            "Description");
        var courseResponse = await Client.PostAsJsonAsync("/api/courses", createCourseRequest);
        Assert.That(courseResponse.IsSuccessStatusCode, Is.True,
            $"Failed to create course: {await courseResponse.Content.ReadAsStringAsync()}");
        var course = await courseResponse.Content.ReadFromJsonAsync<CourseDto>();
        var courseId = course!.Id;

        var getCourseResponse = await Client.GetAsync($"/api/courses/{courseId}");
        Assert.That(getCourseResponse.IsSuccessStatusCode, Is.True, $"Course {courseId} doesn't exist after creation");

        var createGroupRequest = new CreateGroupRequest(
            "Test Group",
            "Group Description",
            courseId,
            teacher.Id);
        var groupResponse = await Client.PostAsJsonAsync("/api/groups", createGroupRequest);
        Assert.That(groupResponse.IsSuccessStatusCode, Is.True,
            $"Failed to create group: {await groupResponse.Content.ReadAsStringAsync()}");
        var group = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = group!.Id;

        var response = await Client.GetAsync("/api/groups");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var groups = await response.Content.ReadFromJsonAsync<List<GroupDto>>();
        Assert.That(groups, Is.Not.Null);
        Assert.That(groups, Has.Count.AtLeast(1));
        Assert.That(groups!.Any(g => g.Id == groupId), Is.True);
    }

    [Test]
    public async Task Admin_CanViewAllGroups()
    {
        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var courseId = await CreateTestCourseAsync();
        var teacher = await GetTeacherAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var response = await Client.GetAsync("/api/groups");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var groups = await response.Content.ReadFromJsonAsync<List<GroupDto>>();
        Assert.That(groups, Is.Not.Null);
        Assert.That(groups, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Student_CanViewGroupsTheyBelongTo()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync("/api/groups");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var groups = await response.Content.ReadFromJsonAsync<List<GroupDto>>();
        Assert.That(groups, Is.Not.Null);
        Assert.That(groups, Has.Count.AtLeast(1));
        Assert.That(groups!.Any(g => g.Id == groupId), Is.True);
    }

    [Test]
    public async Task Student_CannotViewGroupsTheyDoNotBelongTo()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var otherStudent = await CreateAdditionalStudentAsync("outsider@example.com");
        SetBearerToken(otherStudent.Token);

        var response = await Client.GetAsync("/api/groups");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var groups = await response.Content.ReadFromJsonAsync<List<GroupDto>>();
        Assert.That(groups, Is.Not.Null);
        Assert.That(groups!.All(g => g.Id != groupId), Is.True);
    }

    [Test]
    public async Task Teacher_CanViewOwnGroupById()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var response = await Client.GetAsync($"/api/groups/{groupId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var group = await response.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.Id, Is.EqualTo(groupId));
    }

    [Test]
    public async Task Admin_CanViewAnyGroupById()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/groups/{groupId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var group = await response.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
    }

    [Test]
    public async Task Student_CanViewGroupTheyBelongTo()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/groups/{groupId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var group = await response.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.Id, Is.EqualTo(groupId));
    }

    [Test]
    public async Task Student_CannotViewGroupTheyDoNotBelongTo()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var otherStudent = await CreateAdditionalStudentAsync("outsider2@example.com");
        SetBearerToken(otherStudent.Token);

        var response = await Client.GetAsync($"/api/groups/{groupId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetGroup_ReturnsNotFound_ForNonExistentGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/groups/{nonExistentId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Teacher_CanUpdateOwnGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var updateRequest = new UpdateGroupRequest("Updated Name", "Updated Description", null);
        var response = await Client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var group = await response.Content.ReadFromJsonAsync<GroupDto>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.Name, Is.EqualTo("Updated Name"));
        Assert.That(group!.Description, Is.EqualTo("Updated Description"));
    }

    [Test]
    public async Task Admin_CanUpdateAnyGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var updateRequest = new UpdateGroupRequest("Admin Updated", null, null);
        var response = await Client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Teacher_CannotUpdateOtherTeacherGroup()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher1.Id);

        var otherTeacher = await CreateAdditionalTeacherAsync("otherteacher@example.com");
        SetBearerToken(otherTeacher.Token);

        var updateRequest = new UpdateGroupRequest("Should Fail", null, null);
        var response = await Client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CannotUpdateGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var updateRequest = new UpdateGroupRequest("Should Fail", null, null);
        var response = await Client.PutAsJsonAsync($"/api/groups/{groupId}", updateRequest);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanDeleteOwnGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);

        var courseId = await CreateTestCourseAsync();

        var createRequest = new CreateGroupRequest(
            "Temp Group",
            "To be deleted",
            courseId,
            teacher.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/groups", createRequest);
        var tempGroup = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        var tempGroupId = tempGroup!.Id;

        var deleteResponse = await Client.DeleteAsync($"/api/groups/{tempGroupId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Admin_CanDeleteAnyGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var createRequest = new CreateGroupRequest(
            "Temp Group Admin",
            "To be deleted",
            courseId,
            teacher.Id);
        var createResponse = await Client.PostAsJsonAsync("/api/groups", createRequest);
        var tempGroup = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        var tempGroupId = tempGroup!.Id;

        var deleteResponse = await Client.DeleteAsync($"/api/groups/{tempGroupId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Teacher_CannotDeleteOtherTeacherGroup()
    {
        var teacher1 = await GetTeacherAsync();
        SetBearerToken(teacher1.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher1.Id);

        var otherTeacher = await CreateAdditionalTeacherAsync("otherteacher2@example.com");
        SetBearerToken(otherTeacher.Token);

        var deleteResponse = await Client.DeleteAsync($"/api/groups/{groupId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CannotDeleteGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var deleteResponse = await Client.DeleteAsync($"/api/groups/{groupId}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanAddStudentToGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var otherStudent = await CreateAdditionalStudentAsync("otherstudent@example.com");

        var request = new AddStudentToGroupRequest(otherStudent.Id);
        var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Teacher_CannotAddNonStudentToGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var nonStudentId = Guid.NewGuid();
        var request = new AddStudentToGroupRequest(nonStudentId);
        var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Teacher_CannotAddDuplicateStudent()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        var request = new AddStudentToGroupRequest(student.Id);
        var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Admin_CanAddStudentToGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var newStudent = await CreateAdditionalStudentAsync("newstudent@example.com");
        var request = new AddStudentToGroupRequest(newStudent.Id);
        var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Student_CannotAddStudentToGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        SetBearerToken(student.Token);

        var otherStudent = await CreateAdditionalStudentAsync("otherstudent2@example.com");
        var request = new AddStudentToGroupRequest(otherStudent.Id);
        var response = await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanRemoveStudentFromGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        var response = await Client.DeleteAsync($"/api/groups/{groupId}/students/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Teacher_CannotRemoveStudentNotInGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var notInGroupStudent = await CreateAdditionalStudentAsync("notingroup@example.com");
        var response = await Client.DeleteAsync($"/api/groups/{groupId}/students/{notInGroupStudent.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Admin_CanRemoveStudentFromGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var newStudent = await CreateAdditionalStudentAsync("toremove@example.com");
        var addRequest = new AddStudentToGroupRequest(newStudent.Id);
        await Client.PostAsJsonAsync($"/api/groups/{groupId}/students", addRequest);

        var response = await Client.DeleteAsync($"/api/groups/{groupId}/students/{newStudent.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Student_CannotRemoveStudentFromGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.DeleteAsync($"/api/groups/{groupId}/students/{student.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Teacher_CanViewStudentsInGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        var response = await Client.GetAsync($"/api/groups/{groupId}/students");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var students = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.That(students, Is.Not.Null);
        Assert.That(students, Has.Count.AtLeast(1));
    }

    [Test]
    public async Task Student_CanViewStudentsInOwnGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var student = await GetStudentAsync();
        await AddStudentToGroupAsync(groupId, student.Id);

        SetBearerToken(student.Token);
        var response = await Client.GetAsync($"/api/groups/{groupId}/students");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var students = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.That(students, Is.Not.Null);
    }

    [Test]
    public async Task Student_CannotViewStudentsInOtherGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var otherStudent = await CreateAdditionalStudentAsync("outsider3@example.com");
        SetBearerToken(otherStudent.Token);

        var response = await Client.GetAsync($"/api/groups/{groupId}/students");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Admin_CanViewStudentsInAnyGroup()
    {
        var teacher = await GetTeacherAsync();
        SetBearerToken(teacher.Token);
        var courseId = await CreateTestCourseAsync();
        var groupId = await CreateTestGroupAsync(courseId, teacher.Id);

        var admin = await GetAdminAsync();
        SetBearerToken(admin.Token);

        var response = await Client.GetAsync($"/api/groups/{groupId}/students");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Unauthorized_ReturnsUnauthorized()
    {
        ClearAuthorization();
        var response = await Client.GetAsync("/api/groups");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}