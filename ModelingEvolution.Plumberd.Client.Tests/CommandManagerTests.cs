using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.Client.GrpcProxy;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ModelingEvolution.Plumberd.Client.Tests
{
    public class CommandManagerTests
    {
        private CommandManager Sut;
        private readonly ICommandInvoker _commandInvoker;
        private readonly IPlumberRuntime _plumber;
        private readonly ISessionManager _sessionManager;

        

        public CommandManagerTests()
        {
            _commandInvoker = Substitute.For<ICommandInvoker>();
            _plumber = Substitute.For<IPlumberRuntime>();
            _sessionManager = Substitute.For<ISessionManager>();
            var processingUnit = Substitute.For<IProcessingUnit>();
            this._manager = new CommandErrorSubscriptionManager(this._plumber, this._sessionManager);
            Sut = new CommandManager( _manager, _commandInvoker);
            _plumber.RunController(Arg.Do<object>(x => _handler=x), Arg.Any<IProcessingUnitConfig>(), Arg.Do<IEventHandlerBinder>(x => this._binder = x)).Returns(Task.FromResult(processingUnit));
            
        }

        private object _handler;
        private Guid _aggregateId = Guid.NewGuid();
        private Command1 _cmd = new Command1();
        private IEventHandlerBinder _binder;
        private CommandErrorSubscriptionManager _manager;

        [Fact]
        public async Task ExecuteWithCorrelationId()
        {
            IErrorEvent actual = null;
            await Sut.Execute(_aggregateId, _cmd, (cmd, errorEvent) => { actual = errorEvent; });

            var eventException = await Invoke();

            actual.ShouldNotBeNull();
            actual.ShouldBe(eventException);
            await _commandInvoker.Received(1).Execute(_aggregateId, _cmd, null);
            _manager.Count.ShouldBe(1);
        }
        [Fact]
        public async Task ExecuteWithCommandWithGenericErrorType()
        {
            IErrorEvent actual = null;
            Sut.SubscribeErrorHandler<Command1, MyExceptionData>((c, e) => { actual = e; });
            await Sut.Execute(_aggregateId, _cmd);

            var eventException = await Invoke();

            actual.ShouldNotBeNull();
            actual.ShouldBe(eventException);
            await _commandInvoker.Received(1).Execute(_aggregateId, _cmd, null);
            _manager.Count.ShouldBe(1);
        }

        [Fact]
        public async Task Cleanup()
        {
            IErrorEvent actual = null;
            _manager.TimeOut = TimeSpan.Zero;

            await Sut.Execute<Command1, MyExceptionData>(_aggregateId, _cmd, (cmd, errorEvent) => { actual = errorEvent; });

            var eventException = await Invoke();

            actual.ShouldBeNull();
            await _commandInvoker.Received(1).Execute(_aggregateId, _cmd, null);
            _manager.Count.ShouldBe(0);
        }

        [Fact]
        public async Task ExecuteWithGeneric()
        {
            IErrorEvent actual = null;
            Sut.SubscribeErrorHandler((c,e) => actual = e);
            await Sut.Execute(_aggregateId, _cmd);

            var eventException = await Invoke();

            actual.ShouldNotBeNull();
            actual.ShouldBe(eventException);
            await _commandInvoker.Received(1).Execute(_aggregateId, _cmd, null);
            _manager.Count.ShouldBe(1);
        }


        [Fact]
        public async Task ExecuteWithSpecificError()
        {
            IErrorEvent actual = null;
            await Sut.Execute<Command1, MyExceptionData>(_aggregateId, _cmd, (cmd, errorEvent) => { actual = errorEvent; });

            var eventException = await Invoke();

            actual.ShouldNotBeNull();
            actual.ShouldBe(eventException);
            await _commandInvoker.Received(1).Execute(_aggregateId, _cmd, null);
            _manager.Count.ShouldBe(1);
        }

        private async Task< MyExceptionData> Invoke()
        {
            IMetadata m = MetadataFactory.Create(_aggregateId, Guid.NewGuid(), _cmd.Id);
            var eventException = new MyExceptionData();
            await _binder.CreateDispatcher(new NullLoggerFactory())(_handler, m, eventException);
            return eventException;
        }
    }
    public static class MetadataFactory
    {
        public static IMetadata Create(Guid streamId, Guid userId, Guid correlationId)
        {
            IMetadataSchema s = new MetadataSchema();
            s.RegisterSystem(MetadataProperty.Category());
            s.RegisterSystem(MetadataProperty.StreamId());
            UserIdEnricher u = new UserIdEnricher();
            u.RegisterSchema(s);

            CorrelationEnricher c = new CorrelationEnricher();
            c.RegisterSchema(s);

            var m = new Metadata.Metadata(s, true);
            m[MetadataProperty.StreamId()] = streamId;
            m[u.UserIdProperty] = userId;
            m[c.CorrelationId] = correlationId;
            return m;
        }
    }
}