
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pollingService.AddPollingTask(id: 1, interval: 1000, work: () =>
        {
            _logger.LogInformation("Task 1 Something...");
            return Task.CompletedTask;
        });

        _pollingService.AddPollingTask(id: 2, interval: 2000, work: () =>
        {
            _logger.LogInformation("Task 2 Something...");
            return Task.CompletedTask;
        });


        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
