
namespace ContosoWorker;

public class BackgroundPollingTask {
    public long Id { get; set; }
    public int Interval { get; set; }
    public DateTime UpdatedAt {get;set;} = DateTime.MinValue;
}

public class PollingHostedService : IHostedService, IDisposable
{ 
    private static int queueCount = 0; // tracked across multiple threads

    private readonly ILogger<PollingHostedService> _logger; 
    private Dictionary<long, Timer?> _timers = new Dictionary<long, Timer?>();
    private Dictionary<long, object> _locks = new Dictionary<long, object>();
    private readonly List<BackgroundPollingTask> pollingTasks = new List<BackgroundPollingTask>();
    public PollingHostedService(ILogger<PollingHostedService> logger) {
        _logger = logger; 
        pollingTasks.Add(new BackgroundPollingTask { Id = 1, Interval = 3000 }); 
        // pollingTasks.Add(new BackgroundPollingTask { Id = 2, Interval = 2000 }); 
        // pollingTasks.Add(new BackgroundPollingTask { Id = 3, Interval = 3000 }); 
    }

    private void DoWork(object? state) {
        var device = (BackgroundPollingTask)state!;  

        var lockByDevice = _locks.GetValueOrDefault(device.Id);
        var timerById = _timers.GetValueOrDefault(device.Id);

        if(lockByDevice is null) return;
        if(timerById is null) return;

        // Jump out from calls that failed to acquire lock.
        var hasLock = false;

        try{
            int currentQueueCount = Interlocked.Increment(ref queueCount);
            Monitor.TryEnter(lockByDevice, ref hasLock); 
            if (!hasLock)
            {
                _logger.LogWarning("Failing to acquire lock for device " + device.Id.ToString()); 
                _logger.LogWarning("Current waiting queue length: " + currentQueueCount.ToString());
                return;
            } 

            timerById.Change(
                dueTime: Timeout.Infinite, 
                period: Timeout.Infinite); // Stop Timer
            
            // Thread.Sleep(1000);
            _logger.LogInformation(
                "DeviceId: {device}, UpdateAt: {time}", 
                device.Id,
                device.UpdatedAt = DateTime.Now
            ); 


        } finally {
            if (hasLock)
            {
                Monitor.Exit(lockByDevice); 
            } 

            // Restart Timer 
            timerById.Change(
                dueTime: device.Interval, 
                period: device.Interval
            ); 
            
            Interlocked.Decrement(ref queueCount); 
        } 
    } 

    public Task StartAsync(CancellationToken cancellationToken)
    {  

        foreach (var pollingTask in pollingTasks) { 
            var timerById = _timers.GetValueOrDefault(pollingTask.Id);

            if(timerById is not null) continue;
            
            _timers.Add(pollingTask.Id, new Timer(
                callback: DoWork, 
                state: pollingTask, 
                dueTime: TimeSpan.Zero, 
                period: TimeSpan.FromMilliseconds(pollingTask.Interval))
            );

            _locks.Add(pollingTask.Id, new object());
        } 

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Timed Hosted Service is stopping.");

        foreach(var (_, timer) in _timers) {
            timer?.Change(Timeout.Infinite, 0);
        }

        return Task.CompletedTask;
    }
 
    public void Dispose()
    {
        foreach(var (_, timer) in _timers) {
            timer?.Dispose();
        }
    }
}
