using System;
using System.Reflection;
using System.Threading;
using SharedLib.Hosting;
using SharedLib.Rebus;
using SharedLib.Rebus.Configuration;
using SharedLib.Rebus.Extensions;

var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};


await ApplicationService.Run(
    Assembly.GetExecutingAssembly(), "PulsarRebus",
    (configuration, assembly, _) =>
    {
        var config = ConfigFactory.Create(configuration);

        return WorkerHostBuilder.Build<WorkerService>(
            assembly, configuration, (container, _) => container.ConfigureRebus(config, assembly));
    },
    cancellationTokenSource.Token);
