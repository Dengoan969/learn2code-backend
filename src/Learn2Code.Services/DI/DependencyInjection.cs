using Learn2Code.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Learn2Code.Services.DI;

public static class DependencyInjection
{
    public static IServiceCollection RegisterServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var pythonPath = configuration["Python:Path"] ?? "python";
        var sandboxDir = configuration["Python:SandboxDir"] ?? "src/sandbox";

        services.AddScoped<InProcessAstAnalyzer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<InProcessAstAnalyzer>>();
            return new InProcessAstAnalyzer(pythonPath, sandboxDir, logger);
        });

        services.AddScoped<CoreComparisonEngine>();
        services.AddScoped<SubmissionService>();

        services.AddScoped<InProcessSandboxClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<InProcessSandboxClient>>();
            return new InProcessSandboxClient(pythonPath, sandboxDir, logger);
        });

        services.AddScoped<ISandboxClient>(sp => sp.GetRequiredService<InProcessSandboxClient>());
        services.AddScoped<IVerificationEngine>(sp => sp.GetRequiredService<CoreComparisonEngine>());
        services.AddScoped<ILanguageAnalyzer>(sp => sp.GetRequiredService<InProcessAstAnalyzer>());

        return services;
    }
}