using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using FluentAssertions;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.StateTransitioning;
using Shouldly;
using Xunit;
using Xunit.Abstractions; //using ModelingEvolution.Abstractions.Projections;

namespace ModelingEvolution.Plumberd.Tests
{
    public class ModelOne : IModel
    {
        public async Task Given(IMetadata m, Event2 e)
        {

        }
    }
    public class ModelAll : IModel
    {
        public void Given(IMetadata m, Event1 e)
        {

        }

        public IEnumerable<(Guid, IEvent)> Given(IMetadata m, Event4 e)
        {
            yield break;
        }
        public async IAsyncEnumerable<(Guid, IEvent)> Given(IMetadata m, Event5 e)
        {
            yield break;
        }
        public async Task<(Guid, Event4)> Given(IMetadata m, Event6 e)
        {
            return (m.StreamId(), new Event4());
        }
        public async Task<(Guid, IEvent)[]> Given(IMetadata m, Event3 e)
        {
            return new (Guid, IEvent)[] { (m.StreamId(), e) };
        }
        public async Task Given(Guid m, Event2 e)
        {

        }
    }
    public class Event1 : Event { }
    public class Event2 : Event { }
    public class Event3 : Event { }
    public class Event4 : Event { }
    public class Event5 : Event { }
    public class Event6 : Event { }
    public class Command1 : Command { }
    public class Command2 : Command { }
    public class Command3 : Command { }
    public class Command4 : Command { }
    public class Command5 : Command { }
    public class Command6 : Command { }
    public class Command7 : Command { }
    public class Command8 : Command { }
    public class Command9 : Command { }
    public class Command10 : Command { }
    public class Command11 : Command { }
    public class Command12 : Command { }
    public class ComplexProcessingUnit 
    {
        public ModelAll ModelAll { get; private set; }
        public ModelOne ModelOne { get; private set; }

        public async Task When(Guid id, Command2 cmd)
        {
            _log.WriteLine("CommandHandler | Task When(Guid, Command2)");
        }
        public void When(Guid id, Command1 cmd)
        {
            _log.WriteLine("CommandHandler | void When(Guid, Command1)");
        }
        
        public IEnumerable<IEvent> When(Guid id, Command3 cmd)
        {
            _log.WriteLine("CommandHandler | IEnumerable<IEvent> When(Guid, Command3)");
            yield return new Event1();
        }
        public IEnumerable<(Guid,IEvent)> When(Guid id, Command4 cmd)
        {
            _log.WriteLine("CommandHandler | IEnumerable<(Guid,IEvent)> When(Guid, Command4)");
            yield return (id,new Event1());
        }
        public IEvent[] When(Guid id, Command5 cmd)
        {
            _log.WriteLine("CommandHandler | IEvent[] When(Guid, Command5)");
            return new[] {new Event1()};
        }
        public (Guid, IEvent)[] When(Guid id, Command6 cmd)
        {
            _log.WriteLine("CommandHandler | (Guid, IEvent)[] When(Guid, Command6)");
            return new[] {(id, (IEvent)new Event1())};
        }
        public async Task<IEvent[]> When(Guid id, Command7 cmd)
        {
            _log.WriteLine("CommandHandler | Task<IEvent[]> When(Guid, Command7)");
            return new[] { new Event1() };
        }
        public async Task<(Guid, IEvent)[]> When(Guid id, Command8 cmd)
        {
            _log.WriteLine("CommandHandler | Task<(Guid, IEvent)[]> When(Guid, Command8)");
            return new[] { (id, (IEvent)new Event1()) };
        }
        public async Task<Event1> When(Guid id, Command9 cmd)
        {
            _log.WriteLine("CommandHandler | Task<Event1> When(Guid, Command9)");
            return new Event1();
        }
        public Event1 When(Guid id, Command10 cmd)
        {
            _log.WriteLine("CommandHandler | Event1 When(Guid, Command10)");
            return new Event1();
        }
        public async Task<(Guid,Event1)> When(Guid id, Command11 cmd)
        {
            _log.WriteLine("CommandHandler | Task<(Guid,Event1)> When(Guid, Command11)");
            return (id,new Event1());
        }
        public (Guid,Event1) When(Guid id, Command12 cmd)
        {
            _log.WriteLine("CommandHandler | (Guid,Event1) When(Guid, Command12)");
            return (id, new Event1());
        }
        public void Given(Guid m, Event1 e)
        {
            _log.WriteLine("EventHandler | void Given(IMetadata, Event1)");
        }

        public IEnumerable<(Guid, IEvent)> Given(IMetadata m, Event4 e)
        {
            _log.WriteLine("EventHandler | IEnumerable Given(IMetadata, Event4)");
            yield return (m.StreamId(), e);
        }
        public async IAsyncEnumerable<(Guid, IEvent)> Given(IMetadata m, Event5 e)
        {
            _log.WriteLine("EventHandler | IAsyncEnumerable Given(IMetadata, Event5)");
            yield return (m.StreamId(), e);
        }
        public async Task<(Guid, Event4)> Given(IMetadata m, Event6 e)
        {
            _log.WriteLine("EventHandler | Task<Event4> Given(IMetadata, Event6)");
            return (m.StreamId(), new Event4());
        }
        public async Task<(Guid, IEvent)[]> Given(IMetadata m, Event3 e)
        {
            _log.WriteLine("EventHandler | Task<[]> Given(IMetadata, Event3)");
            return new (Guid, IEvent)[] { (m.StreamId(), e) };
        }
        public async Task Given(IMetadata m, Event2 e)
        {
            _log.WriteLine("EventHandler | Task Given(IMetadata, Event2)");
        }

        public async Task<(Guid, Command1)> When(IMetadata m, Event1 e)
        {
            _log.WriteLine("EventHandler | Task<Command1> When(IMetadata, Event1)");
            return (e.Id, new Command1());
        }

        public IEnumerable<(Guid, ICommand)> When(IMetadata m, Event2 e)
        {
            _log.WriteLine("EventHandler | IEnumerable When(IMetadata, Event2)");
            yield return (e.Id, new Command2());
        }
        public async IAsyncEnumerable<(Guid, ICommand)> When(IMetadata m, Event3 e)
        {
            _log.WriteLine("EventHandler | IAsyncEnumerable When(IMetadata, Event3)");
            yield return (e.Id, new Command3());
        }

        private readonly ITestOutputHelper _log;
        public ComplexProcessingUnit(ITestOutputHelper log)
        {
            _log = log;
            ModelAll = new ModelAll();
            ModelOne = new ModelOne();
        }
    }


    public class HandlerBinderTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private HandlerDispatcher Sut;
        private ComplexProcessingUnit controller;
        private IMetadata m;
        public HandlerBinderTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            controller = new ComplexProcessingUnit(_testOutputHelper);
            IMetadataSchema s = new MetadataSchema();
            s.RegisterSystem(MetadataProperty.Category);
            s.RegisterSystem(MetadataProperty.StreamId);

            m = new Metadata.Metadata(s, true);
            m[MetadataProperty.StreamId] = Guid.NewGuid();
        }

        
        [Theory]
        [ClassData(typeof(RecordsData))]
        public async Task Discovery(IRecord r, bool isEmptyEmit)
        {
            HandlerBinder<ComplexProcessingUnit> binder = new HandlerBinder<ComplexProcessingUnit>();
            binder.Discover(true);

            this.Sut = binder.CreateDispatcher();

            var result = await Sut(controller, m, r);
            result.IsEmpty.ShouldBe(isEmptyEmit);
        }
    }
    public class TransitionUnitTests
    {
        [Fact]
        public async Task WhenReturnsEnumerable()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command1());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event1>();
        }

        [Fact]
        public void Given()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }
        [Fact]
        public void GivenMany()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1(), new Event1(), new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }

        [Fact]
        public void WhenReturnsEvent()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command2());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event2>();
        }

    }
    public class ComplexTransitionUnit : StateTransitionUnit<ComplexTransitionUnit, ComplexTransitionUnit.State>
    {
        public struct State
        {
            public string Name { get; set; }
        }

        private static Event2 When(State st, Command2 cmd)
        {
            return new Event2();
        }


        private static IEnumerable<IEvent> When(State st, Command1 cmd)
        {
            yield return new Event1();
        }

        public State GetState() => _state;
        public static State Given(State st, Event1 e)
        {
            st.Name = "Foo";
            return st;
        }

    }
}