using System.Net;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;

namespace Learn2Code.Tests;

[TestFixture]
[Parallelizable]
public class AuthenticationTests : TestBase
{
    [Test]
    public async Task Admin_CanLogin_WithCorrectCredentials()
    {
        // Ensure admin user exists before testing login
        await GetAdminAsync();
        
        var loginRequest = new LoginRequest("admin", "admin123");
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token, Is.Not.Null);
        Assert.That(loginResponse.User, Is.Not.Null);
        Assert.That(loginResponse.User.Login, Is.EqualTo("admin"));
        Assert.That(loginResponse.User.Role, Is.EqualTo("Admin"));
    }

    [Test]
    public async Task Login_Fails_WithWrongPassword()
    {
        // Ensure admin user exists before testing wrong password
        await GetAdminAsync();
        
        var loginRequest = new LoginRequest("admin", "wrongpassword");
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_Fails_WithNonExistentUser()
    {
        var loginRequest = new LoginRequest("nonexistent", "password");
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task AuthenticatedUser_CanGetOwnProfile()
    {
        var token = await GetAdminTokenAsync();
        SetBearerToken(token);

        var response = await Client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var meResponse = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.That(meResponse, Is.Not.Null);
        Assert.That(meResponse!.User, Is.Not.Null);
        Assert.That(meResponse.User.Login, Is.EqualTo("admin"));
    }

    [Test]
    public async Task UnauthenticatedUser_CannotGetProfile()
    {
        var response = await Client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Admin_CanChangeOwnPassword()
    {
        var token = await GetAdminTokenAsync();
        SetBearerToken(token);

        var changeRequest = new ChangePasswordRequest("admin123", "newadmin123");
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        ClearAuthorization();
        var loginRequest = new LoginRequest("admin", "newadmin123");
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetBearerToken(token);
        var revertRequest = new ChangePasswordRequest("newadmin123", "admin123");
        var revertResponse = await Client.PostAsJsonAsync("/api/auth/change-password", revertRequest);
        Assert.That(revertResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ChangePassword_Fails_WithWrongCurrentPassword()
    {
        var token = await GetAdminTokenAsync();
        SetBearerToken(token);

        var changeRequest = new ChangePasswordRequest("wrongpassword", "newpassword");
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", changeRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}