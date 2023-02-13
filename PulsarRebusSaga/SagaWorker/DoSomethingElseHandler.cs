using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using SharedLib.Messages.Commands;
using SharedLib.Messages.Messages;

namespace PulsarRebusSaga.SagaWorker
{
    public class DoSomethingElseHandler : IHandleMessages<DoSomethingElseCommand>
    {
        private readonly IBus _bus;

        public DoSomethingElseHandler(IBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public async Task Handle(DoSomethingElseCommand message)
        {
            Console.WriteLine($"Saga {message.SagaId}: Doing Something Else");
            await Task.Delay(10); // Simulate db call
            await _bus.Reply(new DoSomethingElseResponse { SagaId = message.SagaId, Result = 1 });
        }
    }
}
