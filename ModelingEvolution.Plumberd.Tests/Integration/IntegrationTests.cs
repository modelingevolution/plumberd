using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Integration.Configuration;
using ModelingEvolution.Plumberd.Tests.Models;
using Serilog;
using Serilog.Events;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using EventData = EventStore.ClientAPI.EventData;

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
       

        [Fact]
        public async Task SaveEvent_LoadEvent()
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
            var nStore = (NativeEventStore)plumber.DefaultEventStore;
            var connection = nStore.Connection;

            var events = await connection.ReadStreamEventsForwardAsync($"{category}-{id}", StreamPosition.Start, 10, true);
            events.Events.Length.ShouldBe(0);

            await nStore.LoadEventFromFile("test.bak");

            events = await connection.ReadStreamEventsForwardAsync($"{category}-{id}", StreamPosition.Start, 10, true);
            events.Events.Length.ShouldBe(1);
            var r = events.Events[0].Event;
            r.EventId.ShouldBe(eId);
            r.Data.ShouldBe(bytes);
            r.Metadata.ShouldBe(bytes);
            r.EventStreamId.ShouldBe($"{category}-{id}");
            r.EventType.ShouldBe("Foo");
        }

        private async Task SaveEvents(Guid id, Guid eId, byte[] bytes, string category)
        {
            var plumber = await CreatePlumber();

            var nStore = (NativeEventStore) plumber.DefaultEventStore;
            var connection = nStore.Connection;
           
            var ev = new EventData(eId, "Foo", true, bytes, bytes);
            await connection.AppendToStreamAsync($"{category}-{id}", ExpectedVersion.Any, new EventData[] {ev});

            await nStore.WriteEventsToFile("test.bak");
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

            var nStore = (NativeEventStore)plumber.DefaultEventStore;
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
            var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(_testOutputHelper, LogEventLevel.Verbose)
                .CreateLogger();
            Log.Logger = logger;

            this.server = await EventStoreServer.Start();
            await Task.Delay(2000);

            PlumberBuilder b = new PlumberBuilder()
                .WithLogger(logger)
                .WithDefaultEventStore(x => x.InSecure());

            var plumber = b.Build();
            return plumber;
        }
    }
}