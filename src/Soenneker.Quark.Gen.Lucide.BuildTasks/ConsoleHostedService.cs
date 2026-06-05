using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

/// <summary>
/// Represents the console hosted service.
/// </summary>
public sealed class ConsoleHostedService : IHostedService
{
    private readonly ILogger<ConsoleHostedService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILucideGeneratorRunner _runner;

    private int? _exitCode;

    public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime,
        ILucideGeneratorRunner runner)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _runner = runner;
    }

    /// <summary>
    /// Executes the start async operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                _logger.LogInformation("Starting Soenneker.Quark.Gen.Lucide.BuildTasks...");

                try
                {
                    _exitCode = await _runner.Run(cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled exception");
                    _exitCode = 1;
                }
                finally
                {
                    _appLifetime.StopApplication();
                }
            }, cancellationToken);
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        _logger.LogInformation("Stopping Soenneker.Quark.Gen.Lucide.BuildTasks with exit code {ExitCode}.", Environment.ExitCode);
        return Task.CompletedTask;
    }
}
