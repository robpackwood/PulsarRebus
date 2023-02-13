using System;
using System.Threading.Tasks;
using Rebus.Handlers;
using SharedLib.Messages.Events;

namespace PulsarRebusConsumer.Handlers;

public class AdditionalFirstTestEventHandler : IHandleMessages<FirstTestEvent>
{
    public async Task Handle(FirstTestEvent message)
    {
        await Task.Delay(10); // Simulate db call
        Console.WriteLine($"Additional First Test Event Data: {message.DataValue}");
    }
}
