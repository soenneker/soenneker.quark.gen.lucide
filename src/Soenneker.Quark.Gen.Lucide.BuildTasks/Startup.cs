using Microsoft.Extensions.DependencyInjection;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddDirectoryUtilAsScoped();
        services.AddFileUtilAsScoped();
        services.AddScoped<ILucideGeneratorRunner, LucideGeneratorRunner>();
        services.AddHostedService<ConsoleHostedService>();
    }
}
