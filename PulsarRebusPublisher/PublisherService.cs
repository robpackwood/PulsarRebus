using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using SharedLib.Messages.Events;

namespace PulsarRebusPublisher;

public class PublisherService : BackgroundService
{
    private readonly Lazy<IBus> _busLazy;
    private IBus? _bus;
    private int _sagaCounter;

    public PublisherService(Lazy<IBus> busLazy)
    {
        _busLazy = busLazy ?? throw new ArgumentNullException(nameof(busLazy));
    }

    public int MessageCount { get; set; } = 10;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_bus is null)
            await Task.Factory.StartNew(() => _bus = _busLazy.Value, cancellationToken);

        Console.WriteLine(
            $"Press '1' to publish a test saga and '2' for {MessageCount} standard pub/sub messages. Ctrl+C to exit.");

        var key = Console.ReadKey();

        switch (key.KeyChar)
        {
            case '1':
                await PublishSagaMessage();
                break;
            case '2':
                await PublishStandardMessages();
                break;
        }

        if (!cancellationToken.IsCancellationRequested)
            await ExecuteAsync(cancellationToken);
    }

    private async Task PublishSagaMessage()
    {
        _sagaCounter++;
        Console.WriteLine($"Publishing saga {nameof(StartSaga)}, Id = {_sagaCounter}");
        await _bus!.Publish(new StartSaga { Id = _sagaCounter });
    }

    private async Task PublishStandardMessages()
    {
        for (var counter = 1; counter <= MessageCount; counter++)
        {
            var message = new FirstTestEvent { DataValue = counter };
            Console.WriteLine($"Publishing {nameof(FirstTestEvent)}, DataValue = {counter}");
            await _bus!.Publish(message);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _bus?.Advanced.Workers.SetNumberOfWorkers(0);
        _bus?.Dispose();
        return Task.CompletedTask;
    }
}
