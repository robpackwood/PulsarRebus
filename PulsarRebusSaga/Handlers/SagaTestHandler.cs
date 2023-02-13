using System;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using SharedLib.Messages.Commands;
using SharedLib.Messages.Events;
using SharedLib.Messages.Messages;

namespace PulsarRebusSaga.Handlers
{
    public class SagaTestHandler :
        Saga<TestSagaData>,
        IAmInitiatedBy<StartSaga>,
        IHandleMessages<DoSomethingResponse>,
        IHandleMessages<DoSomethingElseResponse>
    {
        private readonly IBus _bus;

        public SagaTestHandler(IBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public async Task Handle(StartSaga message)
        {
            Console.WriteLine($"Saga {Data.SagaId}: Initialized");

            await Task.WhenAll(
                    _bus.Publish(new DoSomethingCommand { SagaId = Data.SagaId }),
                    _bus.Publish(new DoSomethingElseCommand { SagaId = Data.SagaId }));
        }

        public Task Handle(DoSomethingElseResponse message)
        {
            Console.WriteLine($"Saga {message.SagaId}: Handled DoSomethingElseResponse");
            Data.DidSomethingElse = true;
            Post();
            return Task.CompletedTask;
        }

        public Task Handle(DoSomethingResponse message)
        {
            Console.WriteLine($"Saga {message.SagaId}: Handled DoSomethingResponse");
            Data.DidSomething = true;
            Post();
            return Task.CompletedTask;
        }

        protected override void CorrelateMessages(ICorrelationConfig<TestSagaData> config)
        {
            config.Correlate<StartSaga>(x => x.Id, testSagaData => testSagaData.SagaId);
            config.Correlate<DoSomethingResponse>(x => x.SagaId, testSagaData => testSagaData.SagaId);
            config.Correlate<DoSomethingElseResponse>(x => x.SagaId, testSagaData => testSagaData.SagaId);
        }

        private void Post()
        {
            if (!Data.IsDone)
                return;

            MarkAsComplete();
        }
    }

    public class TestSagaData : SagaData
    {
        public int SagaId { get; set; }
        public bool DidSomething { get; set; }
        public bool DidSomethingElse { get; set; }
        public bool IsDone => DidSomething && DidSomethingElse;
    }
}
