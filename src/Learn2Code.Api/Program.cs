using System.Text;
using Learn2Code.Api.Middleware;
using Learn2Code.Core;
using Learn2Code.Core.Entities;
using Learn2Code.Infrastructure.Data;
using Learn2Code.Infrastructure.DI;
using Learn2Code.Services.DI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "dev_secret"))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("TeacherOrAdmin", policy => policy.RequireRole("Teacher", "Admin"));
    options.AddPolicy("StudentOrAbove", policy => policy.RequireRole("Student", "Teacher", "Admin"));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use centralized JsonOptions from Core project
        var defaultOptions = JsonOptions.Default;
        options.JsonSerializerOptions.ReferenceHandler = defaultOptions.ReferenceHandler;
        options.JsonSerializerOptions.DefaultIgnoreCondition = defaultOptions.DefaultIgnoreCondition;
        options.JsonSerializerOptions.PropertyNamingPolicy = defaultOptions.PropertyNamingPolicy;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = defaultOptions.PropertyNameCaseInsensitive;

        // Copy converters
        options.JsonSerializerOptions.Converters.Clear();
        foreach (var converter in defaultOptions.Converters) options.JsonSerializerOptions.Converters.Add(converter);
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString))
    builder.Services.RegisterInfrastructure(connectionString);
else
    throw new InvalidOperationException(
        "Database connection string is not configured. Please set 'ConnectionStrings:DefaultConnection' in appsettings.json");

var sandboxDir = builder.Configuration["Python:SandboxDir"] ?? "src/sandbox";
if (!Path.IsPathRooted(sandboxDir))
{
    // exeDir = .../edu/src/Learn2Code.Api/bin/Debug/net8.0/
    var exeDir = AppContext.BaseDirectory;
    // repoRoot = .../edu/
    var repoRoot = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "..", ".."));
    sandboxDir = Path.GetFullPath(Path.Combine(repoRoot, sandboxDir));
}

builder.Configuration["Python:SandboxDir"] = sandboxDir;
builder.Services.RegisterServices(builder.Configuration);

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Learn2Code API",
        Version = "v1",
        Description = "API для образовательной платформы Learn2Code"
    });

    // Добавляем поддержку JWT авторизации в Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Learn2Code API V1");
        c.RoutePrefix = "swagger"; // Доступ по адресу /swagger
        c.DisplayRequestDuration(); // Показывать время выполнения запросов
        c.DocExpansion(DocExpansion.List); // Развернутый вид по умолчанию
    });
}

// Middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Skip seed data during tests or when explicitly configured
var skipSeed = builder.Configuration.GetValue<bool>("Database:SkipSeed") ||
               Environment.GetEnvironmentVariable("SKIP_SEED") == "true";

if (!skipSeed)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            db.Database.EnsureCreated();
            var userCount = db.Users.Count();
            Console.WriteLine($"Seed: Database has {userCount} users");

            if (!db.Users.Any())
            {
                Console.WriteLine("Seed: Creating admin user");
                var adminId = Guid.NewGuid();
                db.Users.Add(new User
                {
                    Id = adminId,
                    Login = "admin",
                    DisplayName = "Администратор",
                    Role = UserRole.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    CreatedAt = DateTime.UtcNow
                });

                var teacherId = Guid.NewGuid();
                db.Users.Add(new User
                {
                    Id = teacherId,
                    Login = "teacher",
                    DisplayName = "Тестовый преподаватель",
                    Role = UserRole.Teacher,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("teacher123"),
                    CreatedAt = DateTime.UtcNow
                });

                var courseId = Guid.NewGuid();
                db.Courses.Add(new Course
                {
                    Id = courseId,
                    Title = "Введение в программирование",
                    Description = "Базовый курс по основам программирования на Python",
                    TeacherId = teacherId,
                    CreatedAt = DateTime.UtcNow
                });

                var lessonId = Guid.NewGuid();
                db.Lessons.Add(new Lesson
                {
                    Id = lessonId,
                    Title = "Первая программа",
                    Description = "Напишите программу, которая выводит 'Hello, World!'",
                    Order = 1,
                    CourseId = courseId
                });

                var studentId = Guid.NewGuid();
                db.Users.Add(new User
                {
                    Id = studentId,
                    Login = "student",
                    DisplayName = "Тестовый студент",
                    Role = UserRole.Student,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("student123"),
                    CreatedAt = DateTime.UtcNow
                });

                var groupId = Guid.NewGuid();
                db.Groups.Add(new Group
                {
                    Id = groupId,
                    Name = "Группа 1",
                    Description = "Тестовая группа для курса 'Введение в программирование'",
                    CourseId = courseId,
                    TeacherId = teacherId,
                    CreatedAt = DateTime.UtcNow
                });

                db.GroupStudents.Add(new GroupStudent
                {
                    GroupId = groupId,
                    StudentId = studentId,
                    JoinedAt = DateTime.UtcNow
                });

                db.SaveChanges();
                Console.WriteLine("Seed: Admin, teacher, student, course, lesson, and group created");
            }
            else
            {
                Console.WriteLine("Seed: Admin user already exists");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seed: Error during seeding: {ex.Message}");
            Console.WriteLine("Seed: Continuing without seed data");
        }
    }
else
    Console.WriteLine("Seed: Skipping seed data (Database:SkipSeed = true or SKIP_SEED=true)");

app.Run();

public partial class Program
{
}