namespace ContosoWorker;

public class BackgroundPollingTask
{
    public long Id { get; init; }
    public int IntervalMs { get; init; }
    public Func<Task> Work { get; init; }
    public DateTime UpdatedAt { get; private set; }

    public BackgroundPollingTask(long id, int intervalMs, Func<Task> work)
    {
        Id = id;
        IntervalMs = intervalMs;
        Work = work;
        UpdatedAt = DateTime.MinValue;
    }

    public void UpdateTime()
    {
        UpdatedAt = DateTime.Now;
    }
}

public interface IPollingHostedService
{
    bool AddPollingTask(long id, int intervalMs, Func<Task> pollingTask);
    bool RemovePollingTask(long id);
}

public class PollingHostedService : IHostedService, IDisposable, IPollingHostedService
{
    private static int _queueCount = 0;
    private readonly ILogger<PollingHostedService> _logger;

    // Wrap! _timer, _locks, _pollingTasks to one class
    private Dictionary<long, Timer> _timers = new Dictionary<long, Timer>();
    private Dictionary<long, object> _locks = new Dictionary<long, object>();
    private readonly List<BackgroundPollingTask> _pollingTasks = new List<BackgroundPollingTask>();

    public PollingHostedService(ILogger<PollingHostedService> logger)
    {
        _logger = logger;
    }

    private void DoWork(object? state)
    {
        var pollingTask = (BackgroundPollingTask)state!;
        var lockByDevice = _locks.GetValueOrDefault(pollingTask.Id);
        var timerById = _timers.GetValueOrDefault(pollingTask.Id);

        if (lockByDevice is null) return;
        if (timerById is null) return;

        var hasLock = false;

        try
        {
            int currentQueueCount = Interlocked.Increment(ref _queueCount);

            Monitor.TryEnter(lockByDevice, ref hasLock);

            if (!hasLock)
            {
                _logger.LogWarning("Failing to acquire lock for device " + pollingTask.Id.ToString());
                _logger.LogWarning("Current waiting queue length: " + currentQueueCount.ToString());
                return;
            }

            timerById.Change(
                dueTime: Timeout.Infinite,
                period: Timeout.Infinite
            );

            if (pollingTask.Work is not null)
            {
                pollingTask.Work
                    .Invoke()
                    .Wait();

                pollingTask.UpdateTime();
            }

        }
        finally
        {
            if (hasLock)
            {
                Monitor.Exit(lockByDevice);
            }

            timerById.Change(
                dueTime: pollingTask.IntervalMs,
                period: pollingTask.IntervalMs
            );

            Interlocked.Decrement(ref _queueCount);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Polling Hosted Service is starting.");

        foreach (var pollingTask in _pollingTasks)
        {
            var timerById = _timers.GetValueOrDefault(pollingTask.Id);

            if (timerById is not null)
            {
                _logger.LogWarning("TaskId: {id} already exists. Ignore to add new polling task.", pollingTask.Id);
                continue;
            }

            _timers.Add(pollingTask.Id, new Timer(
                callback: DoWork,
                state: pollingTask,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromMilliseconds(pollingTask.IntervalMs))
            );

            _locks.Add(pollingTask.Id, new object());
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Polling Hosted Service is stopping.");

        foreach (var (_, timer) in _timers)
        {
            timer?.Change(Timeout.Infinite, 0);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var (_, timer) in _timers)
        {
            timer?.Dispose();
        }
    }


    public bool RemovePollingTask(long id)
    {
        var timerById = _timers.GetValueOrDefault(id);
        if (timerById is null)
        {
            _logger.LogWarning("TaskId: {id} not exists", id);
            return false;
        }

        timerById.Change(Timeout.Infinite, 0);
        timerById.Dispose();

        var isSuccessToRemoveTimerSyncLock = _locks.Remove(id);
        var isSuccessToRemoveTimer = _timers.Remove(id);
        var removedTaskCount = _pollingTasks.RemoveAll(x => x.Id == id);

        return true;
    }

    public bool AddPollingTask(long id, int intervalMs, Func<Task> pollingTask)
    {
        var newTask = new BackgroundPollingTask(id, intervalMs, pollingTask);

        var timerById = _timers.GetValueOrDefault(newTask.Id);

        if (timerById is not null)
        {
            _logger.LogWarning("TaskId: {id} already exists", newTask.Id);
            return false;
        }

        _timers.Add(id, new Timer(
            callback: DoWork,
            state: newTask,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(newTask.IntervalMs))
        );

        _locks.Add(newTask.Id, new object());

        _pollingTasks.Add(newTask);

        return true;
    }

}
