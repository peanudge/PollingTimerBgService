
namespace ContosoWorker;

public class MainLoopService : IHostedService
{
    private readonly PollingService _pollingService;
    private readonly ILogger<MainLoopService> _logger;
    public MainLoopService(IServiceProvider serviceProvider, ILogger<MainLoopService> logger)
    {
        _pollingService = serviceProvider.GetRequiredService<PollingService>();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _pollingService.AddPollingTask(id: 1, interval: 1000, work: (cts) =>
        {
            _logger.LogInformation("Task 1 Something... {time}", DateTime.Now);
            return Task.CompletedTask;
        });

        _pollingService.AddPollingTask(id: 2, interval: 1000, work: (cts) =>
        {
            _logger.LogInformation("Task 2 Something... {time}", DateTime.Now);
            return Task.CompletedTask;
        });

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(3000, cancellationToken);

            _logger.LogInformation("Stop Task 1, 2 - {time}", DateTime.Now);
            _pollingService.StopPollingTask(1);
            _pollingService.StopPollingTask(2);

            await Task.Delay(3000, cancellationToken);

            _logger.LogInformation("Restart Task 1, 2 - {time}", DateTime.Now);
            _pollingService.RestartPollingTask(1);
            _pollingService.RestartPollingTask(2);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
