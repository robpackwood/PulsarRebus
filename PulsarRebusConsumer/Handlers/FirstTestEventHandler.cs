using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using SharedLib.Messages.Events;

namespace PulsarRebusConsumer.Handlers;

public class FirstTestEventHandler : IHandleMessages<FirstTestEvent>
{
    private readonly IBus _bus;

    public FirstTestEventHandler(IBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    }

    public async Task Handle(FirstTestEvent message)
    {
        await Task.Delay(10); // Simulate db call
        Console.WriteLine($"First Test Event Data: {message.DataValue}");
        var secondTestEvent = new SecondTestEvent { DataValue = message.DataValue };
        await _bus.Publish(secondTestEvent);
    }
}
