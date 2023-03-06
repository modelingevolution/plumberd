using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.ClientAPI;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Integration.Configuration;
using ModelingEvolution.Plumberd.Tests.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using EventData = EventStore.ClientAPI.EventData;
using StreamPosition = EventStore.ClientAPI.StreamPosition;

namespace ModelingEvolution.Plumberd.Tests.Integration
{
    public enum CommunicationProtocol
    {
        Tcp,
        Grpc
    };
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private EventStoreServer server;

        public IntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }
        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Tcp)]
        [InlineData(CommunicationProtocol.Grpc)]
        public async Task InvokeCommand_HandleCommand_WithException(CommunicationProtocol protocol)
        {
            Guid id = Guid.NewGuid();
            var c = new CommandRaisingException();

            var plumber = await CreatePlumber(protocol);
            plumber.RegisterController(new CommandHandlerWithExceptions());
            await plumber.StartAsync();

            await plumber.DefaultCommandInvoker.Execute(id, c);
            
            await Task.Delay(5000);
        }
        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Tcp)]
        [InlineData(CommunicationProtocol.Grpc)]
        public async Task InvokeCommand_HandleCommand_CheckCommandStream(CommunicationProtocol protocol)
        {
            Guid id = Guid.NewGuid();
            FooCommand c = new FooCommand();
          
            var plumber = await CreatePlumber(protocol);

            await plumber.DefaultCommandInvoker.Execute(id, c);
            
            var context = new CommandProcessingContextFactory().Create(this, new FooCommand());

            using (StaticProcessingContext.CreateScope(context))
            {
                var cmd = (await plumber.DefaultEventStore.GetCommandStream<FooCommand>(id).ReadEvents()
                        .ToArrayAsync())
                    .OfType<FooCommand>()
                    .FirstOrDefault();

                cmd.Id.ShouldBe(c.Id);
                cmd.Name.ShouldBe(c.Name);
            }
           

        }

        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Grpc)]
        [InlineData(CommunicationProtocol.Tcp)]
        public async Task SaveEvent_LoadEvent( CommunicationProtocol protocol)
        {
            string category = "Foo";
            Guid id = Guid.NewGuid();
            Guid eId = Guid.NewGuid();
            var bytes = Encoding.UTF8.GetBytes("{ }");

            await SaveEvents(id, eId, bytes, category,protocol);

            await server.StartInDocker();

            await LoadAndAssert(id, eId, bytes, category, protocol);
        }

        
        private async Task LoadAndAssert(Guid id, Guid eId, byte[] bytes, string category, CommunicationProtocol protocol)
        {

            var plumber = await CreatePlumber(protocol);
            if (protocol == CommunicationProtocol.Tcp)
            {
                var nStore = (EventStore.NativeEventStore) plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var events =
                    await connection.ReadStreamEventsForwardAsync($"{category}-{id}", StreamPosition.Start, 10, true);
                events.Events.Length.ShouldBe(0);

                await nStore.LoadEventFromFile("test.bak");

                events = await connection.ReadStreamEventsForwardAsync($"{category}-{id}", StreamPosition.Start, 10,
                    true);
                events.Events.Length.ShouldBe(1);
                var r = events.Events[0].Event;
                r.EventId.ShouldBe(eId);
                r.Data.ShouldBe(bytes);
                r.Metadata.ShouldBe(bytes);
                r.EventStreamId.ShouldBe($"{category}-{id}");
                r.EventType.ShouldBe("Foo");
            }
            else
            {
             
                var nStore = (EventStore.GrpcEventStore)plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var events =
                     connection.ReadStreamAsync(Direction.Forwards, $"{category}-{id}", StreamPosition.Start, maxCount: 10);
                  events.GetAsyncEnumerator().Current.Event.ShouldBe(null);
                await nStore.LoadEventFromFile("test.bak");

                events =  connection.ReadStreamAsync(Direction.Forwards,$"{category}-{id}", StreamPosition.Start, 10);
                var d = await events.GetAsyncEnumerator().MoveNextAsync();
                d.ShouldNotBe(false);
                var r = events.Current.Event;
                r.EventId.ShouldBe(Uuid.FromGuid(eId));
                r.Data.ShouldBe(bytes);
                r.Metadata.ShouldBe(bytes);
                r.EventStreamId.ShouldBe($"{category}-{id}");
                r.EventType.ShouldBe("Foo");
            }
        }

        private async Task SaveEvents(Guid id, Guid eId, byte[] bytes, string category, CommunicationProtocol protocol)
        {
            var plumber = await CreatePlumber(protocol);
            if (protocol == CommunicationProtocol.Tcp)
            {
                var nStore = (NativeEventStore) plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var ev = new EventData(eId, "Foo", true, bytes, bytes);
                await connection.AppendToStreamAsync($"{category}-{id}", ExpectedVersion.Any, new EventData[] {ev});

                await nStore.WriteEventsToFile("test.bak"); //TODO put into separate interface
            }
            else
            {
                var nStore = (GrpcEventStore)plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var ev = new global::EventStore.Client.EventData(Uuid.FromGuid(eId), "Foo",bytes,bytes);

                await connection.AppendToStreamAsync($"{category}-{id}", StreamRevision.None, new global::EventStore.Client.EventData[]{ev},userCredentials:new UserCredentials("admin","changeit"));
                

                await nStore.WriteEventsToFile("test.bak");
            }
        }
        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Tcp)]
        public async Task InvokeErrorCommand_IgnoreCorrellationId(CommunicationProtocol proto)
        {
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler();

            var plumber = await CreatePlumber(proto);
            plumber.RegisterController(projection);
            plumber.RegisterController(commandHandler);
            await plumber.StartAsync();

            /* Invoking new command */
            Guid id = Guid.NewGuid();
            var faultyCommand = new FaultyCommand();
            var goodCommand = new FooCommand();
            await plumber.DefaultCommandInvoker.Execute(id, faultyCommand);
            await plumber.DefaultCommandInvoker.Execute(id, new IgnoreByCorrelationId(){CorrelationId = faultyCommand.Id});
            await plumber.DefaultCommandInvoker.Execute(id, goodCommand);

            await Task.Delay(2000);
            /* Waiting 1 sec for the command-handler and event-handler to finish processing. */
            projection.Count.ShouldBe(1);

        }

        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Grpc)]
        [InlineData(CommunicationProtocol.Tcp)]
        public async Task InvokeCommand_HandleCommand_HandleEvent_CorrelationCheck(CommunicationProtocol protocol)
        {
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler();

            var plumber = await CreatePlumber(protocol);
            plumber.RegisterController(projection);
            plumber.RegisterController(commandHandler);
            await plumber.StartAsync();

            /* Invoking new command */
            Guid id = Guid.NewGuid();
            FooCommand c = new FooCommand();
            await plumber.DefaultCommandInvoker.Execute(id, c);

            /* Waiting 1 sec for the command-handler and event-handler to finish processing. */
            
            Thread.Sleep(1000);
            var correlationStream = plumber.DefaultEventStore.GetCorrelationStream(c.Id, ContextScope.Command);

            IRecord[] records = null;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            do
            {
                records = await correlationStream.ReadEvents().ToArrayAsync();
            }
            while (records.Length < 2 && sw.Elapsed < TimeSpan.FromSeconds(10));

            records.Length.ShouldBe(2);
            records[0].ShouldBeOfType<FooCommand>();
            records[1].ShouldBeOfType<FooEvent>();
            ((FooCommand)records[0]).Name.ShouldBe(c.Name);
            ((FooCommand)records[0]).Id.ShouldBe(c.Id);
            ((FooEvent)records[1]).Id.ShouldBe(commandHandler.ReturningEvent.Id);
        }
        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Grpc)]
        [InlineData(CommunicationProtocol.Tcp)]
        public async Task InvokeCommand_HandleCommand_HandleEvent_LinkCheck(CommunicationProtocol protocol)
        {
            Guid id = Guid.NewGuid();
            FooCommand command = new FooCommand();
            FooEvent ev = new FooEvent();

            var projection = new FooLinkProjection();
            var commandHandler = new FooCommandHandler() { ReturningEvent = ev };

            var plumber = await CreatePlumber(protocol);
            plumber.RegisterController(projection);
            plumber.RegisterController(commandHandler);
            await plumber.StartAsync();

            await plumber.DefaultCommandInvoker.Execute(id, command);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                await Task.Delay(100);
            } while (projection.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(30));

            var nStore = plumber.DefaultEventStore;
            await Task.Delay(500);
            var eventHandlerContext = NSubstitute.Substitute.For<IEventHandlerContext>();
            var data = await nStore.GetStream("/FooLink", projection.StreamId, eventHandlerContext).Read().ToArrayAsync();
            data.Length.ShouldBe(1);
            projection.Event.ShouldNotBeNull();
            data[0].Item2.ShouldBeEquivalentTo(projection.Event);

        }
        [Trait("Category", "Integration")]
        [Theory]
        [InlineData(CommunicationProtocol.Grpc)]
        [InlineData(CommunicationProtocol.Tcp)]
        public async Task InvokeCommand_HandleCommand_HandleEvent_MetadataCheck(CommunicationProtocol protocol)
        {
            Guid id = Guid.NewGuid();
            FooCommand command = new FooCommand();
            FooEvent ev = new FooEvent();
            
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler() { ReturningEvent = ev};

            var plumber = await CreatePlumber(protocol);
            plumber.RegisterController(projection);
            plumber.RegisterController(commandHandler);
            await plumber.StartAsync();

            await plumber.DefaultCommandInvoker.Execute(id, command);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                await Task.Delay(100);
            } while (projection.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(10));

            projection.Count.ShouldBe(1);
            projection.Metadata.ShouldNotBeNull();
            projection.Event.ShouldNotBeNull();
            projection.Event.Id.ShouldBe(ev.Id);
            projection.Metadata.Category().ShouldBe("Foo");
            projection.Metadata.StreamId().ShouldBe(id);
            projection.Metadata.StreamPosition().ShouldBe(0UL);
            projection.Metadata.CausationId().ShouldBe(command.Id);
            projection.Metadata.CorrelationId().ShouldBe(command.Id);
            projection.Metadata.Created().ShouldBe(DateTimeOffset.Now, TimeSpan.FromSeconds(5));

        }
        private async Task<IPlumberRuntime> CreatePlumber(CommunicationProtocol protocol)
        {

            server = await EventStoreServer.Start();
            await Task.Delay(2000);

            if (protocol == CommunicationProtocol.Tcp)
            {
                PlumberBuilder b = new PlumberBuilder()
                    .WithLoggerFactory(new NullLoggerFactory())
                    .WithTcpEventStore(x => x.InSecure()
                        .WithTcpUrl(server.TcpUrl)
                        .WithHttpUrl(server.HttpUrl))
                    .WithVersion(new Version(1,2));

                var plumber = b.Build();
                return plumber;
            }
            else
            {
                PlumberBuilder b = new PlumberBuilder()
                    .WithLoggerFactory(new NullLoggerFactory())
                    .WithGrpcEventStore(x => x.InSecure()
                        .WithHttpUrl(server.HttpUrl));

                var plumber = b.Build();
                return plumber;
            }
        }
    }
}