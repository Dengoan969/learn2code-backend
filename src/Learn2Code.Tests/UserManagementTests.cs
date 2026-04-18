using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class UserManagementTests : TestBase
{
    [Test]
    public async Task Admin_CanCreateNewUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var login = $"testuser{Guid.NewGuid()}";
        var createRequest = new CreateUserRequest(login, "password123", "Test User", "Student");

        var response = await Client.PostAsJsonAsync("/api/users", createRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Login, Is.EqualTo(login));
        Assert.That(user.DisplayName, Is.EqualTo("Test User"));
        Assert.That(user.Role, Is.EqualTo("Student"));
    }

    [Test]
    public async Task Admin_CanGetAllUsers()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var response = await Client.GetAsync("/api/users");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.That(users, Is.Not.Null);
        Assert.That(users!, Has.Some.Matches<UserDto>(u => u.Login == "admin"));
    }

    [Test]
    public async Task Admin_CanGetUserById()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var usersResponse = await Client.GetAsync("/api/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var admin = users!.First(u => u.Login == "admin");

        var response = await Client.GetAsync($"/api/users/{admin.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Id, Is.EqualTo(admin.Id));
        Assert.That(user.Login, Is.EqualTo("admin"));
    }

    [Test]
    public async Task Admin_CanUpdateUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var login = $"testupdate{Guid.NewGuid()}";
        var createRequest = new CreateUserRequest(login, "password123", "Original Name", "Student");
        var createResponse = await Client.PostAsJsonAsync("/api/users", createRequest);

        Assert.That(createResponse.IsSuccessStatusCode, Is.True,
            $"User creation failed: {createResponse.StatusCode} - {await createResponse.Content.ReadAsStringAsync()}");

        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var updateRequest = new UpdateUserRequest(login, "Updated Name", "Teacher");

        var response = await Client.PutAsJsonAsync($"/api/users/{createdUser!.Id}", updateRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updatedUser = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.That(updatedUser, Is.Not.Null);
        Assert.That(updatedUser!.DisplayName, Is.EqualTo("Updated Name"));
        Assert.That(updatedUser.Role, Is.EqualTo("Teacher"));
    }

    [Test]
    public async Task Admin_CanResetUserPassword()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var login = $"testreset{Guid.NewGuid()}";
        var createRequest = new CreateUserRequest(login, "original123", "Test User", "Student");
        var createResponse = await Client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var resetRequest = new ResetPasswordRequest("newpassword123");

        var response = await Client.PostAsJsonAsync($"/api/users/{createdUser!.Id}/reset-password", resetRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        ClearAuthorization();
        var loginRequest = new LoginRequest(login, "newpassword123");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Admin_CanDeleteUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetBearerToken(adminToken);

        var login = $"testdelete{Guid.NewGuid()}";
        var createRequest = new CreateUserRequest(login, "password123", "Test User", "Student");
        var createResponse = await Client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var response = await Client.DeleteAsync($"/api/users/{createdUser!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/users/{createdUser.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Teacher_CannotCreateUser()
    {
        var teacherToken = await GetTeacherTokenAsync();
        SetBearerToken(teacherToken);

        var createRequest = new CreateUserRequest("test@example.com", "password123", "Test User", "Student");

        var response = await Client.PostAsJsonAsync("/api/users", createRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Student_CannotAccessUserManagement()
    {
        var studentToken = await GetStudentTokenAsync();
        SetBearerToken(studentToken);

        var response = await Client.GetAsync("/api/users");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}