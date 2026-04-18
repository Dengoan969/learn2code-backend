using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learn2Code.Core.DTOs;
using Learn2Code.Core.Entities;
using Learn2Code.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learn2Code.Tests;

// Пользователь для тестов с токеном и ID
public record TestUser(string Token, Guid Id, string Login, UserRole Role);

[TestFixture]
public abstract class TestBase : IDisposable
{
    protected static readonly System.Text.Json.JsonSerializerOptions JsonOptions = Learn2Code.Core.JsonOptions.Default;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testDbName = $"test_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        
        // Create test database connection string
        var baseConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;Include Error Detail=true";
        _testConnectionString = baseConnectionString.Replace("Database=postgres", $"Database={_testDbName}");

        // First, create the test database using the default postgres database
        using (var connection = new Npgsql.NpgsqlConnection(baseConnectionString))
        {
            connection.Open();
            using (var command = new Npgsql.NpgsqlCommand($"CREATE DATABASE {_testDbName}", connection))
            {
                command.ExecuteNonQuery();
            }
        }

        // Create database schema before building the factory
        using (var context = CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Configure app settings to skip seed data during tests
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:SkipSeed"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_testConnectionString, npgsql => npgsql.EnableRetryOnFailure(
                        3,
                        TimeSpan.FromSeconds(5),
                        null)));
            });
        });

        Client = Factory.CreateClient();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_testConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Drop test database after all tests
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureDeleted();
        }
        
        Dispose();
    }

    [SetUp]
    public async Task SetUp()
    {
        ClearAuthorization();
        
        // Clear cached users since we're about to delete data
        _cachedAdmin = null;
        _cachedTeacher = null;
        _cachedStudent = null;
        _additionalTeachers.Clear();
        _additionalStudents.Clear();
        
        // Clear all data between tests
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            try
            {
                // Clear all tables in correct order of dependencies
                // Start with most dependent tables
                dbContext.Submissions.RemoveRange(dbContext.Submissions);
                dbContext.Progress.RemoveRange(dbContext.Progress);
                dbContext.GroupStudents.RemoveRange(dbContext.GroupStudents);
                dbContext.Tasks.RemoveRange(dbContext.Tasks);
                dbContext.Lessons.RemoveRange(dbContext.Lessons);
                dbContext.Groups.RemoveRange(dbContext.Groups);
                dbContext.Courses.RemoveRange(dbContext.Courses);
                dbContext.Users.RemoveRange(dbContext.Users);
                
                await dbContext.SaveChangesAsync();
                
                // Debug: log counts after cleanup
                Console.WriteLine($"DEBUG: After cleanup - Courses: {dbContext.Courses.Count()}, Users: {dbContext.Users.Count()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during test cleanup: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }

    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    private string _testDbName = null!;
    private string _testConnectionString = null!;

    private TestUser? _cachedAdmin;
    private TestUser? _cachedTeacher;
    private TestUser? _cachedStudent;
    private readonly List<TestUser> _additionalTeachers = new();
    private readonly List<TestUser> _additionalStudents = new();

    protected async Task<TestUser> GetAdminAsync()
    {
        if (_cachedAdmin != null)
            return _cachedAdmin;

        await EnsureAdminExistsAsync();

        var loginRequest = new LoginRequest("admin", "admin123");
        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        // Debug: check login response
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"DEBUG: Admin login failed with status: {response.StatusCode}");
            Console.WriteLine($"DEBUG: Login response content: {content}");
            response.EnsureSuccessStatusCode();
        }
        
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        var token = loginResponse?.Token ?? throw new InvalidOperationException("Token not found");

        var adminId = await GetUserIdFromTokenAsync(token);
        
        // Verify the admin user exists in the database
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbAdmin = await dbContext.Users.FindAsync(adminId);
            Console.WriteLine($"DEBUG: Admin user from DB: {dbAdmin != null} (ID: {adminId})");
            if (dbAdmin == null)
            {
                Console.WriteLine("DEBUG: Admin user not found in database! Listing all users:");
                var allUsers = await dbContext.Users.ToListAsync();
                foreach (var user in allUsers)
                {
                    Console.WriteLine($"  - {user.Login} ({user.Id})");
                }
            }
        }

        _cachedAdmin = new TestUser(token, adminId, "admin", UserRole.Admin);
        return _cachedAdmin;
    }

    protected async Task<TestUser> GetTeacherAsync()
    {
        if (_cachedTeacher != null)
            return _cachedTeacher;

        var teacherLogin = "teacher";
        var teacherToken = await CreateUserIfNotExistsAsync(teacherLogin, "Teacher", "teacher123");
        var teacherId = await GetUserIdFromTokenAsync(teacherToken);

        _cachedTeacher = new TestUser(teacherToken, teacherId, teacherLogin, UserRole.Teacher);
        return _cachedTeacher;
    }

    protected async Task<TestUser> GetStudentAsync()
    {
        if (_cachedStudent != null)
            return _cachedStudent;

        var studentLogin = "student";
        var studentToken = await CreateUserIfNotExistsAsync(studentLogin, "Student", "student123");
        var studentId = await GetUserIdFromTokenAsync(studentToken);

        _cachedStudent = new TestUser(studentToken, studentId, studentLogin, UserRole.Student);
        return _cachedStudent;
    }

    protected async Task<TestUser> CreateAdditionalTeacherAsync(string? customLogin = null)
    {
        var login = customLogin ?? $"teacher{Guid.NewGuid()}";
        var token = await CreateUserAndGetTokenAsync("Teacher", login);
        var userId = await GetUserIdFromTokenAsync(token);

        var teacher = new TestUser(token, userId, login, UserRole.Teacher);
        _additionalTeachers.Add(teacher);
        return teacher;
    }

    protected async Task<TestUser> CreateAdditionalStudentAsync(string? customLogin = null)
    {
        var login = customLogin ?? $"student{Guid.NewGuid()}";
        var token = await CreateUserAndGetTokenAsync("Student", login);
        var userId = await GetUserIdFromTokenAsync(token);

        var student = new TestUser(token, userId, login, UserRole.Student);
        _additionalStudents.Add(student);
        return student;
    }

    private async Task EnsureAdminExistsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check if admin already exists
        var existingAdmin = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Login == "admin");
        
        if (existingAdmin != null)
        {
            Console.WriteLine($"DEBUG: Admin user already exists with ID: {existingAdmin.Id}");
            return;
        }

        Console.WriteLine("DEBUG: Creating admin user...");
        
        // Create admin user
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Login = "admin",
            DisplayName = "Administrator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();
        
        Console.WriteLine($"DEBUG: Admin user created with ID: {admin.Id}");
        
        // Verify the user was saved
        var savedAdmin = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Login == "admin");
        Console.WriteLine($"DEBUG: After save - Admin user exists: {savedAdmin != null}");
    }

    private async Task<string> CreateUserIfNotExistsAsync(string login, string role, string password)
    {
        var loginRequest = new LoginRequest(login, password);
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (loginResponse.IsSuccessStatusCode)
        {
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return loginResult?.Token ?? throw new InvalidOperationException("Token not found");
        }

        return await CreateUserAndGetTokenAsync(role, login);
    }

    protected async Task<string> CreateUserAndGetTokenAsync(string role, string? login = null)
    {
        var originalAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            login ??= $"test{Guid.NewGuid()}";
            var password = "test123";

            var admin = await GetAdminAsync();
            SetBearerToken(admin.Token);

            var createRequest = new CreateUserRequest(login, password, "Test User", role);
            var response = await Client.PostAsJsonAsync("/api/users", createRequest);
            response.EnsureSuccessStatusCode();

            ClearAuthorization();
            var loginRequest = new LoginRequest(login, password);
            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return loginResult?.Token ?? throw new InvalidOperationException("Token not found");
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = originalAuth;
        }
    }

    private async Task<Guid> GetUserIdFromTokenAsync(string token)
    {
        var originalAuth = Client.DefaultRequestHeaders.Authorization;
        try
        {
            SetBearerToken(token);
            var response = await Client.GetAsync("/api/auth/me");
            response.EnsureSuccessStatusCode();
            var meResponse = await response.Content.ReadFromJsonAsync<MeResponse>();
            return meResponse?.User?.Id ?? throw new InvalidOperationException("User ID not found");
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = originalAuth;
        }
    }

    protected void SetBearerToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthorization()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    protected async Task<Guid> GetCurrentUserIdAsync(string token)
    {
        return await GetUserIdFromTokenAsync(token);
    }

    protected async Task<string> GetAdminTokenAsync()
    {
        var admin = await GetAdminAsync();
        return admin.Token;
    }

    protected async Task<string> GetTeacherTokenAsync()
    {
        var teacher = await GetTeacherAsync();
        return teacher.Token;
    }

    protected async Task<string> GetStudentTokenAsync()
    {
        var student = await GetStudentAsync();
        return student.Token;
    }

    protected Task<AppDbContext> GetDbContextAsync()
    {
        var scope = Factory.Services.CreateScope();
        return Task.FromResult(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}