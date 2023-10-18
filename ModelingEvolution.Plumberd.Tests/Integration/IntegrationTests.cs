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
using EventData = EventStore.Client.EventData;
using StreamPosition = EventStore.Client.StreamPosition;

namespace ModelingEvolution.Plumberd.Tests.Integration
{
    
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private EventStoreServer server;

        public IntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }
        [Trait("Category", "Integration")]
        [Fact]
        public async Task InvokeCommand_HandleCommand_WithException()
        {
            Guid id = Guid.NewGuid();
            var c = new CommandRaisingException();

            var plumber = await CreatePlumber();
            plumber.RegisterController(new CommandHandlerWithExceptions());
            await plumber.StartAsync();

            await plumber.DefaultCommandInvoker.Execute(id, c);
            
            await Task.Delay(5000);
        }
        [Trait("Category", "Integration")]
        [Fact]
        public async Task InvokeCommand_HandleCommand_CheckCommandStream()
        {
            Guid id = Guid.NewGuid();
            FooCommand c = new FooCommand();
          
            var plumber = await CreatePlumber();

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
        [Fact]
        public async Task SaveEvent_LoadEvent( )
        {
            string category = "Foo";
            Guid id = Guid.NewGuid();
            Guid eId = Guid.NewGuid();
            var bytes = Encoding.UTF8.GetBytes("{ }");

            await SaveEvents(id, eId, bytes, category);

            await server.StartInDocker();

            await LoadAndAssert(id, eId, bytes, category);
        }

        
        private async Task LoadAndAssert(Guid id, Guid eId, byte[] bytes, string category)
        {

            var plumber = await CreatePlumber();
            
             
                var nStore = (EventStore.NativeEventStore)plumber.DefaultEventStore;
                var connection = nStore.Connection;

                var events =
                     connection.ReadStreamAsync(Direction.Forwards, $"{category}-{id}", StreamPosition.Start, maxCount: 10);
                (await events.ReadState).ShouldBe(ReadState.StreamNotFound);

                await nStore.LoadEventFromFile("test.bak");

                events =  connection.ReadStreamAsync(Direction.Forwards,$"{category}-{id}", StreamPosition.Start, 10);
                (await events.ReadState).ShouldBe(ReadState.Ok);
                
                var r = (await events.FirstOrDefaultAsync()).Event;
                r.EventId.ShouldBe(Uuid.FromGuid(eId));
                r.Data.ShouldBe(bytes);
                r.Metadata.ShouldBe(bytes);
                r.EventStreamId.ShouldBe($"{category}-{id}");
                r.EventType.ShouldBe("Foo");

        }

        private async Task SaveEvents(Guid id, Guid eId, byte[] bytes, string category)
        {
            var plumber = await CreatePlumber();

            var nStore = (NativeEventStore)plumber.DefaultEventStore;
            var connection = nStore.Connection;

            var ev = new global::EventStore.Client.EventData(Uuid.FromGuid(eId), "Foo", bytes, bytes);

            await connection.AppendToStreamAsync($"{category}-{id}", StreamRevision.None, new global::EventStore.Client.EventData[] { ev }, userCredentials: new UserCredentials("admin", "changeit"));


            await nStore.WriteEventsToFile("test.bak");

        }
        [Trait("Category", "Integration")]
        [Fact]
        
        public async Task InvokeErrorCommand_IgnoreCorrellationId()
        {
            var projection = new FooProjection();
            var commandHandler = new FooCommandHandler();

            var plumber = await CreatePlumber();
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

            server = await EventStoreServer.Start();
            await Task.Delay(2000);


            PlumberBuilder b = new PlumberBuilder()
                .WithLoggerFactory(new NullLoggerFactory())
                .WithGrpc(x => x.InSecure()

                    .WithHttpUrl(server.HttpUrl))
                .WithVersion(new Version(1, 2));

            var plumber = b.Build();
            return plumber;

        }
    }
}