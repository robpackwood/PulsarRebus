using System;
using System.Threading.Tasks;
using Rebus.Handlers;
using SharedLib.Messages.Events;

namespace PulsarRebusConsumer.Handlers;

public class SecondTestEventHandler : IHandleMessages<SecondTestEvent>
{
    public async Task Handle(SecondTestEvent message)
    {
        await Task.Delay(10); // Simulate db call
        Console.WriteLine($"Second Test Event Data: {message.DataValue}");
    }
}
