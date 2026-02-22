using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Quark.Gen.Lucide.BuildTasks.Abstract;

namespace Soenneker.Quark.Gen.Lucide.BuildTasks;

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

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    _exitCode = await _runner.Run(cancellationToken);
                }
                catch (Exception e)
                {
                    var message = $"Soenneker.Quark.Gen.Lucide.BuildTasks: Unhandled exception: {e}";
                    _logger.LogError(e, "Unhandled exception");
                    await Console.Error.WriteLineAsync(message);
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

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        return Task.CompletedTask;
    }
}
