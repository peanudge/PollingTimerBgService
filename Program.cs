using ContosoWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // TODO: Add services
    })
    .Build();

host.Run();
