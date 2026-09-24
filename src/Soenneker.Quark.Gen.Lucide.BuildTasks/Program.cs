using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

/// <summary>
/// Represents the program.
/// </summary>
public sealed class Program
{
    private static CancellationTokenSource? _cts;

    /// <summary>
    /// Runs the application using the supplied command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A task that completes when the application exits.</returns>
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Console.Error.WriteLine($"Fatal: {ex}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"UnobservedTaskException: {e.Exception}");
            e.SetObserved();
        };

        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddConsole());
            Startup.ConfigureServices(services);

            await using ServiceProvider provider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            Environment.ExitCode = await scope.ServiceProvider.GetRequiredService<ILucideGeneratorRunner>().Run(_cts.Token);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            Environment.ExitCode = 130;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Stopped program because of exception: {e}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            _cts?.Dispose();
        }
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _cts?.Cancel();
    }

}
