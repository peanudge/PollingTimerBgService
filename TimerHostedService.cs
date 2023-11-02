
namespace ContosoWorker;

public class Device {
    public long Id { get; set; }
    public int Interval { get; set; }
    public DateTime UpdatedAt {get;set;} = DateTime.MinValue;
}

public class TimedHostedService : IHostedService, IDisposable
{ 
    private static int queueCount = 0; // tracked across multiple threads

    private readonly ILogger<TimedHostedService> _logger; 
    private Dictionary<long, Timer?> _timers = new Dictionary<long, Timer?>();
    private Dictionary<long, object> _locks = new Dictionary<long, object>();
    private readonly List<Device> devices = new List<Device>();
    public TimedHostedService(ILogger<TimedHostedService> logger) {
        _logger = logger; 
        devices.Add(new Device { Id = 1, Interval = 1000 });
        // devices.Add(new Device { Id = 2, Interval = 1000 });
        // devices.Add(new Device { Id = 3, Interval = 2000 });
    }

    private void DoWork(object? state) {
        var device = (Device)state!;  

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

            timerById.Change(Timeout.Infinite, Timeout.Infinite); // Stop Tim
            
            Thread.Sleep(1000);
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
            timerById.Change(0, device.Interval); // Restart Timer 
            Interlocked.Decrement(ref queueCount); 
        } 
    } 

    public Task StartAsync(CancellationToken cancellationToken)
    {  

        foreach (var device in devices) { 
            var timerById = _timers.GetValueOrDefault(device.Id);

            if(timerById is not null) continue;
            
            _timers.Add(device.Id, new Timer(
                callback: DoWork, 
                state: device, 
                dueTime: TimeSpan.Zero, 
                period: TimeSpan.FromMilliseconds(device.Interval))
            );

            _locks.Add(device.Id, new object());
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
