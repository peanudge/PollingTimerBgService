namespace ContosoWorker;

public class PollingTask
{
    public long Id { get; init; }
    public int Interval { get; init; }
    public Func<CancellationToken, Task> Work { get; init; }
    public Timer Timer { get; set; } = null!;
    public object TimerLock { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public PollingTask(long id, int interval, Func<CancellationToken, Task> work)
    {
        Id = id;
        Interval = interval;
        Work = work;
    }

    public void AttachTimer(Timer timer)
    {
        Timer = timer;
        TimerLock = new object();
    }
}

public interface IPollingService
{
    bool AddPollingTask(long id, int interval, Func<CancellationToken, Task> work);
    bool RemovePollingTask(long id);
    bool StopPollingTask(long id);
    bool RestartPollingTask(long id);
}

public class PollingService : IHostedService, IDisposable, IPollingService
{
    private readonly ILogger<PollingService> _logger;
    private readonly List<PollingTask> _pollingTasks = new List<PollingTask>();
    private CancellationToken _cancellationToken = CancellationToken.None;

    public PollingService(ILogger<PollingService> logger)
    {
        _logger = logger;
    }

    private void TimerCallback(object? state)
    {
        var pollingTask = (PollingTask)state!;
        var timerLock = pollingTask.TimerLock;
        var timer = pollingTask.Timer;

        if (timerLock is null) return;
        if (timer is null) return;

        var hasLock = false;

        try
        {
            Monitor.TryEnter(timerLock, ref hasLock);

            if (!hasLock)
            {
                _logger.LogWarning("Failing to acquire lock: " + pollingTask.Id.ToString());
                return;
            }

            timer.Change(
                dueTime: Timeout.Infinite,
                period: Timeout.Infinite
            );

            if (pollingTask.Work is not null)
            {
                pollingTask.Work
                    .Invoke(pollingTask.CancellationToken)
                    .Wait();
            }
        }
        finally
        {
            if (hasLock)
            {
                Monitor.Exit(timerLock);
            }

            timer.Change(
                dueTime: pollingTask.Interval,
                period: pollingTask.Interval
            );
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        _logger.LogInformation("Polling Hosted Service is starting.");

        foreach (var pollingTask in _pollingTasks)
        {
            var timerById = pollingTask.Timer;

            if (timerById is not null)
            {
                _logger.LogWarning("TaskId: {id} already exists. Ignore to add new polling task.", pollingTask.Id);
                continue;
            }

            var newTimer = new Timer(
                callback: TimerCallback,
                state: pollingTask,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromMilliseconds(pollingTask.Interval)
            );

            pollingTask.AttachTimer(newTimer);
            pollingTask.CancellationToken = cancellationToken;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Polling Hosted Service is stopping.");

        foreach (var pollingTask in _pollingTasks)
        {
            pollingTask.Timer?.Change(Timeout.Infinite, 0);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var pollingTask in _pollingTasks)
        {
            pollingTask.Timer?.Dispose();
        }
    }

    public bool RemovePollingTask(long id)
    {
        var pollingTask = _pollingTasks.Where(t => t.Id == id).FirstOrDefault();
        if (pollingTask is null)
        {
            _logger.LogWarning("PollingTaskId: {id} not exists", id);
            return false;
        }

        var timer = pollingTask.Timer;
        timer.Change(Timeout.Infinite, 0);
        timer.Dispose();

        var removedTaskCount = _pollingTasks.RemoveAll(x => x.Id == id);

        return removedTaskCount >= 1;
    }

    public bool AddPollingTask(long id, int interval, Func<CancellationToken, Task> work)
    {
        var pollingTask = _pollingTasks.Where(t => t.Id == id).FirstOrDefault();

        if (pollingTask is not null)
        {
            _logger.LogWarning("PollingTaskId: {id} already exists", id);
            return false;
        }

        var newPollingTask = new PollingTask(id, interval, work);

        _pollingTasks.Add(newPollingTask);

        var newTimer = new Timer(
            callback: TimerCallback,
            state: newPollingTask,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMilliseconds(interval)
        );

        newPollingTask.AttachTimer(newTimer);
        newPollingTask.CancellationToken = _cancellationToken;
        return true;
    }

    public bool StopPollingTask(long id)
    {
        var pollingTask = _pollingTasks.Where(t => t.Id == id).FirstOrDefault();
        if (pollingTask is null)
        {
            _logger.LogWarning("PollingTaskId: {id} not exists", id);
            return false;
        }

        var timer = pollingTask.Timer;
        return timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public bool RestartPollingTask(long id)
    {
        var pollingTask = _pollingTasks.Where(t => t.Id == id).FirstOrDefault();
        if (pollingTask is null)
        {
            _logger.LogWarning("PollingTaskId: {id} not exists", id);
            return false;
        }

        var timer = pollingTask.Timer;
        return timer.Change(pollingTask.Interval, pollingTask.Interval);
    }
}
