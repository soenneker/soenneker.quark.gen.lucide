using Microsoft.Extensions.DependencyInjection;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILucideGeneratorRunner, LucideGeneratorRunner>();
        services.AddHostedService<ConsoleHostedService>();
    }
}
