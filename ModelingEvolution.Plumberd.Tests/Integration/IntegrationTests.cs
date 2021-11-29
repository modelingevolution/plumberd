using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.ClientAPI;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Integration.Configuration;
using ModelingEvolution.Plumberd.Tests.Models;
using Microsoft.Extensions.Logging;
using Modellution.Logging;
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

        [Theory]
        [InlineData(CommunicationProtocol.Tcp)]
        [InlineData(CommunicationProtocol.Grpc)]
        public async Task InvokeCommand_HandleCommand_WithException(CommunicationProtocol protocol)
        {
            Guid id = Guid.NewGuid();
            var c = new CommandRaisingException();

            var plumber = await CreatePlumber();
            plumber.RegisterController(new CommandHandlerWithExceptions());
            await plumber.StartAsync();

            await plumber.DefaultCommandInvoker.Execute(id, c);

            await Task.Delay(200000);
        }

        [Fact]
        public async Task InvokeCommand_HandleCommand_CheckCommandStream()
        {
            Guid id = Guid.NewGuid();
            FooCommand c = new FooCommand();

            var plumber = await CreatePlumber();

            await plumber.DefaultCommandInvoker.Execute(id, c);
            
            var context = new CommandProcessingContextFactory().Create(this, id, new FooCommand());

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

            var plumber = await CreatePlumber();
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
                //TODO ask about DefaultEventStore (if forget - it was making up Native by default)
                var nStore = new GrpcEventStoreBuilder().Build(false);
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
            var plumber = await CreatePlumber();
            if (protocol == CommunicationProtocol.Tcp)
            {
                var nStore = (NativeEventStore) plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var ev = new EventData(eId, "Foo", true, bytes, bytes);
                await connection.AppendToStreamAsync($"{category}-{id}", ExpectedVersion.Any, new EventData[] {ev});

                await nStore.WriteEventsToFile("test.bak");
            }
            else
            {
                var nStore = new GrpcEventStoreBuilder().Build(true);
                var connection = nStore.Connection;

                var ev = new global::EventStore.Client.EventData(Uuid.FromGuid(eId), "Foo",bytes,bytes);

                await connection.AppendToStreamAsync($"{category}-{id}", StreamRevision.None, new global::EventStore.Client.EventData[]{ev});

                await nStore.WriteEventsToFile("test.bak");
            }
        }


        [Fact]
        public async Task InvokeCommand_HandleCommand_HandleEvent_CorrelationCheck()
        {
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler();

            var plumber = await CreatePlumber();
            plumber.RegisterController(projection);
            plumber.RegisterController(commandHandler);
            await plumber.StartAsync();

            /* Invoking new command */
            Guid id = Guid.NewGuid();
            FooCommand c = new FooCommand();
            await plumber.DefaultCommandInvoker.Execute(id, c);

            /* Waiting 1 sec for the command-handler and event-handler to finish processing. */
            

            var correlationStream = plumber.DefaultEventStore.GetCorrelationStream(c.Id);

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

        [Fact]
        public async Task InvokeCommand_HandleCommand_HandleEvent_LinkCheck()
        {
            Guid id = Guid.NewGuid();
            FooCommand command = new FooCommand();
            FooEvent ev = new FooEvent();

            var projection = new FooLinkProjection();
            var commandHandler = new FooCommandHandler() { ReturningEvent = ev };

            var plumber = await CreatePlumber();
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

            var nStore = plumber.DefaultEventStore;
            await Task.Delay(200);
            var eventHandlerContext = NSubstitute.Substitute.For<IEventHandlerContext>();
            var data = await nStore.GetStream("/FooLink", projection.StreamId, eventHandlerContext).Read().ToArrayAsync();
            data.Length.ShouldBe(1);
            projection.Event.ShouldNotBeNull();
            data[0].Item2.ShouldBeEquivalentTo(projection.Event);

        }
        [Fact]
        public async Task InvokeCommand_HandleCommand_HandleEvent_MetadataCheck()
        {
            Guid id = Guid.NewGuid();
            FooCommand command = new FooCommand();
            FooEvent ev = new FooEvent();
            
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler() { ReturningEvent = ev};

            var plumber = await CreatePlumber();
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
        
        private async Task<IPlumberRuntime> CreatePlumber()
        {
            

            this.server = await EventStoreServer.Start();
            await Task.Delay(2000);

            PlumberBuilder b = new PlumberBuilder()
                .WithDefaultEventStore(x => x.InSecure());

            var plumber = b.Build();
            return plumber;
        }
    }
}