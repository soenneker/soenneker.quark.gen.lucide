using Microsoft.Extensions.DependencyInjection;

namespace Soenneker.Quark.Gen.Lucide.Demo;

/// <summary>
/// Registers services for the app.
/// </summary>
public static class BuildTimeServices
{
    public static void Configure(IServiceCollection services, string baseAddress)
    {
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseAddress) });
    }
}
