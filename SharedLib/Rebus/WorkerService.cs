using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;

namespace SharedLib.Rebus;

public class WorkerService : BackgroundService
{
    private readonly Lazy<IBus> _busLazy;
    private IBus? _bus;

    public WorkerService(Lazy<IBus> busLazy)
    {
        _busLazy = busLazy;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(() =>
        {
            try
            {
                _bus = _busLazy.Value;
            }
            catch
            {
                Environment.FailFast("Error creating Rebus endpoint");
            }
        }, cancellationToken);
    }

	public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.Factory.StartNew(
            () =>
            {
                _bus?.Advanced.Workers.SetNumberOfWorkers(0);
                _bus?.Dispose();
            }, cancellationToken);
    }
}
