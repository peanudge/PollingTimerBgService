
using System.Data.Common;

namespace ContosoWorker;


// INFO: Limitation: It is hard to change timer state
public class PerriodicPollingTask
{
    public long Id { get; set; }
    public int IntervalMs { get; set; }
    public Func<Task> Work { get; set; }
    public PerriodicPollingTask(long id, Func<Task> work, int intervalMs)
    {
        Id = id;
        IntervalMs = intervalMs;
        Work = work;
    }
}

public class PeriodicPollingService : BackgroundService
{
    private readonly ILogger<PeriodicPollingService> _logger;

    public List<PerriodicPollingTask> PollingTasks { get; set; } = new List<PerriodicPollingTask>();


    public PeriodicPollingService(ILogger<PeriodicPollingService> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timed Hosted Service running.");

        foreach (var pollingTask in PollingTasks)
        {
            Task.Run(async () =>
            {
                await RunPollingTask(pollingTask, stoppingToken);
            });
        }

        return Task.CompletedTask;
    }

    // Could also be a async method, that can be awaited in ExecuteAsync above
    private void DoWork(long id)
    {
        _logger.LogInformation("Time Hosted Service is working. id: {id}, {Date}", id, DateTime.Now);
    }

    private Task RunPollingTask(PerriodicPollingTask pollingTask, CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollingTask.IntervalMs));
            // TODO: It is hard to change timer periodic
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    // if this task has long time, it will block the next task
                    DoWork(pollingTask.Id);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Timed Hosted Service is stopping.");
            }
        });
    }
}
