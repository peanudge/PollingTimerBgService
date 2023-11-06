# Polling data using Timer

> Reference
> https://gunnarpeipman.com/avoid-overlapping-timer-calls/ > https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-7.0&tabs=visual-studio

Timers are useful .NET feature. With timers we can execute code with specified interval. With real scenarios we can easily face situations where timer calls its callback again but previous call is not finished yet. **It may end up with loading some external system too much.** It may also end up by making unexpected parallel data processing that distorts the results.

This post introduces some options to avoid overlapping timer calls.

# Simple Timer

simple class that hosts timer that fires callback after every ten seconds.

```csharp
public class TimerService : IHostedService, IDisposable
{
    private static Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(Callback, null, 0, 10000);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
    }

    public void Callback(object state)
    {
        // Do something
    }
}
```

We have no controls for overlapping calls here.
If callback is not finished after ten seconds then next next call to it is made by timer in parallel.

# Using lock

If we want to avoid overlapping then first and simplest idea to come out with is locking the part of callback body where actual work is done.

```csharp
public class TimerService : IHostedService, IDisposable
{
    private static object _lock = new object();
    private static Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(Callback, null, 0, 10000);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
    }

    public void Callback(object state)
    {
        lock(_lock) {
            // Do something
        }
    }
}
```

if callback takes minute to execute for some unexpected reason then there will be six calls waiting after lock

# Using lock with bypassing task failed to acquire lock

```csharp
public class TimerService : IHostedService, IDisposable
{
    private const long TimerInterval = 1000;
    private static object _lock = new object();
    private static Timer _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(Callback, null, 0, TimerInterval);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Dispose();
    }

    public void Callback(object state)
    {
        var hasLock = false;

        try
        {
            Monitor.TryEnter(_locker, ref hasLock);
            if (!hasLock)
            {
                return;
            }

            _timer.Change(Timeout.Infinite, Timeout.Infinite); // Stop Timer

            // Do something

        }
        finally
        {
            if (hasLock)
            {
                Monitor.Exit(_locker);
                _timer.Change(TimerInterval, TimerInterval); // Restart Timer
            }
        }
    }
}
```

Now we almost got rid of overlapping calls but not completely.

Calls to timer callback still happen but they quit fast when failing to acquire lock.

This method is also good for cases when some work in timer callback must be done at every call and there’s also some work that can’t be done in parallel by multiple threads.

# PeriodTimer.cs

> https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-7.0

After .NET 6.0, we can PeriodTimer without implementing manually lock between overlaped timer callback.
but, We can not access directly timer in PeriodTimer. So, i manually implements lock between invoking timer callback
