using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Serialization;
using Serilog;

namespace ModelingEvolution.Plumberd.EventStore
{
   
    public interface IEventStore
    {
        IEventStoreSettings Settings { get; }
        IStream GetStream(string category, Guid id, IContext context = null);
        
        Task Subscribe(string name, 
            bool fromBeginning, 
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory processingContextFactory,
            params string[] sourceEventTypes);
    }
    public interface IEventStoreSettings
    {
        IMetadataFactory MetadataFactory { get; }
        IMetadataSerializerFactory MetadataSerializerFactory { get; }
        IRecordSerializer Serializer { get; }
        string ProjectionStreamPrefix { get; }
        string CommandStreamPrefix { get; }
        Func<Type,string[]> RecordNamingConvention { get; }
        ILogger Logger { get; }
    }
}