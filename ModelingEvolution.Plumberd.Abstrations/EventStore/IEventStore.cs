using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using ProtoBuf;
using Serilog;

namespace ModelingEvolution.Plumberd.EventStore
{
    public interface ISubscription : IDisposable
    {

    }
    
    /// <summary>
    /// This cannot be invoked from Client! So no PROTO-CONTRACT!
    /// </summary>
    public class UploadBlob : ICommand, IStreamAware
    {
        public Guid Id { get; set; }
        public long Size { get; set; }
        public string Name { get; set; }
        
        public UploadBlob()
        {
            Id = Guid.NewGuid();
        }
        // should switch to enums at least.
        public string StreamCategory { get; set; }
        public BlobUploadReason Reason { get; set; }
    }

    public class IncludedAsAttribute : Attribute
    {
        public IncludedAsAttribute(int fieldNumber)
        {
            FieldNumber = fieldNumber;
        }

        public int FieldNumber { get; private set; }
    }
    
    [ProtoContract]
    //[ProtoInclude(100, typeof(ImageBlockUploaded))]
    [JsonConverter(typeof(JsonInheritanceConverter<BlobUploadReason>))]
    public abstract class BlobUploadReason
    {

    }
    [ProtoContract]
    public class BlobUploaded : IEvent, IStreamAware
    {
        [ProtoMember(1)]
        public Guid Id { get; set; }
        
        [ProtoMember(2)]
        public long Size { get; set; }
        
        [ProtoMember(3)]
        public string Name { get; set; }

        [ProtoMember(4)]
        public string StreamCategory { get; set; }

        public TReason GetReason<TReason>() where TReason : BlobUploadReason
        {
            return (TReason)Reason;}

        [ProtoMember(5)]
        public BlobUploadReason Reason { get; set; }
        public string Url(IMetadata m)
        {
            return $"/blob/{m.Category()}-{m.StreamId()}";
        }
        public BlobUploaded()
        {
            Id = Guid.NewGuid();
        }
    }
    public interface IEventStore
    {
        IEventStoreSettings Settings { get; }
        IStream GetStream(string category, Guid id, IContext context = null);
        Task Init();
        Task<ISubscription> Subscribe(ProjectionSchema schema, 
            bool fromBeginning, 
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory);
        Task<ISubscription> Subscribe(string name,
            bool fromBeginning,
            bool isPersistent,
            EventHandler onEvent,
            IProcessingContextFactory factory,
            params string[] types);
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
    public class ProjectionSchema
    {
        public bool IsDirect => string.IsNullOrWhiteSpace(ProjectionName);
        public string ProjectionName { get; set; }
        public string StreamName { get; set; }
        public string Script { get; set; }
    }
}