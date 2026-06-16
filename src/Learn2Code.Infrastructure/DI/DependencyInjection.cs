using Learn2Code.Core.Interfaces;
using Learn2Code.Infrastructure.Data;
using Learn2Code.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learn2Code.Infrastructure.DI;

public static class DependencyInjection
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
                3,
                TimeSpan.FromSeconds(5),
                null)));


        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IProgressRepository, ProgressRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();

        return services;
    }
}