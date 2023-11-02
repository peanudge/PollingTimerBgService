# Polling data using Timer

> Reference
> https://gunnarpeipman.com/avoid-overlapping-timer-calls/

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