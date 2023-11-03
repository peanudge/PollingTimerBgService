using ContosoWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<PollingHostedService>();
        services.AddHostedService(
            (serviceProvider) =>
                serviceProvider.CreateScope().ServiceProvider.GetRequiredService<PollingHostedService>());

        services.AddHostedService<MainLoopService>();
    })
    .Build();

host.Run();
