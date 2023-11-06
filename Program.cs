using ContosoWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // TODO: Check if this is a good idea
        // services.AddSingleton<PeriodicPollingService>();
        // services.AddHostedService(
        //     (serviceProvider) =>
        //         serviceProvider.CreateScope().ServiceProvider.GetRequiredService<PeriodicPollingService>());

        services.AddSingleton<PollingService>();
        services.AddHostedService(
            (serviceProvider) =>
                serviceProvider.CreateScope().ServiceProvider.GetRequiredService<PollingService>());

        services.AddHostedService<MainLoopService>();
    })
    .Build();

host.Run();
