
namespace ContosoWorker;

public class MainLoopService : IHostedService
{
    private readonly PollingHostedService _pollingHostedService;
    private readonly ILogger<MainLoopService> _logger;
    public MainLoopService(IServiceProvider serviceProvider, ILogger<MainLoopService> logger)
    {
        _pollingHostedService = serviceProvider.GetRequiredService<PollingHostedService>();
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pollingHostedService.AddPollingTask(id: 1, intervalMs: 1000, pollingTask: () =>
        {
            _logger.LogInformation("Task 1 Something...");
            return Task.CompletedTask;
        });

        _pollingHostedService.AddPollingTask(id: 2, intervalMs: 2000, pollingTask: () =>
        {
            _logger.LogInformation("Task 2 Something...");
            return Task.CompletedTask;
        });

        _pollingHostedService.AddPollingTask(id: 3, intervalMs: 3000, pollingTask: () =>
        {
            _logger.LogInformation("Task 3 Something...");
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // throw new NotImplementedException();
        return Task.CompletedTask;
    }
}
