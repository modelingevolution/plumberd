using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventStore
{
    
    public interface IStream
    {
        IEventStore EventStore { get; }
        string Category { get; }
        Guid Id { get; }

        Task Append(IRecord ev, IMetadata m);

        IAsyncEnumerable<IRecord> Read();
    }
    public static class StaticStreamExtensions 
    {
        public static async Task Append(this IStream stream, IRecord x, IContext context = null)
        {
            context ??= StaticProcessingContext.Context;

            var factory = stream.EventStore.Settings.MetadataFactory;
            //var metadata = context switch
            //{
            //    ICommandHandlerContext cpc => factory.Create(cpc, x),
            //    IEventHandlerContext epc => factory.Create(epc, x),
            //    _ => throw new NoContextAvailableException()
            //};
            var metadata = factory.Create(x, context);
            await stream.Append(x, metadata);
        }

        public static async Task Append(this IStream stream, IEnumerable<IRecord> events)
        {
            var context = StaticProcessingContext.Context;
            foreach (var ev in events)
                await stream.Append(ev, context);
        }
    }
    public class StaticProcessingContext
    {
        private class Disposable : IDisposable
        {
            private Action a;

            public Disposable(Action a)
            {
                this.a = a;
            }

            public void Dispose()
            {
                a();
            }
        }
        private static AsyncLocal<IContext> _context;
        
        public static IDisposable CreateScope(IProcessingContext c)
        {
            if(_context == null)
                _context = new AsyncLocal<IContext>();
            _context.Value = c;
            return new Disposable(() => _context.Value = null);
        }
        public static IContext Context => _context?.Value;
    }
}