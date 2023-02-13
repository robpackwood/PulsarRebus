using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using SharedLib.Messages.Commands;
using SharedLib.Messages.Messages;

namespace PulsarRebusSaga.SagaWorker
{
    public class DoSomethingHandler : IHandleMessages<DoSomethingCommand>
    {
        private readonly IBus _bus;

        public DoSomethingHandler(IBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public async Task Handle(DoSomethingCommand message)
        {
            Console.WriteLine($"Saga {message.SagaId}: Doing Something");
            await Task.Delay(10); // Simulate db call
            await _bus.Reply(new DoSomethingResponse { SagaId = message.SagaId, Result = 1 });
        }
    }
}
