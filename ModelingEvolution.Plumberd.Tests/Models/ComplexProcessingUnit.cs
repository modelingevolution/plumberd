using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;
using Xunit.Abstractions;

namespace ModelingEvolution.Plumberd.Tests.Models
{
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
}