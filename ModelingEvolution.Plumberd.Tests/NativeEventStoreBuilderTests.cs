using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class NativeEventStoreBuilderTests
    {
        public enum CommunicationProtocol
        {
            Tcp,
            Grpc
        };
        [InlineData(CommunicationProtocol.Tcp)]
        [InlineData(CommunicationProtocol.Grpc)]
        [Theory]
        public void Build(CommunicationProtocol protocol)
        {
            if(protocol == CommunicationProtocol.Grpc)
            {
                GrpcEventStoreBuilder s = new GrpcEventStoreBuilder()
                .WithMetadataFactory(new MetadataFactory())
                .WithMetadataSerializerFactory(new MetadataSerializerFactory())
                .WithRecordSerializer(new RecordSerializer());}
            else
            {
                NativeEventStoreBuilder s = new NativeEventStoreBuilder()
                    .WithMetadataFactory(new MetadataFactory())
                    .WithMetadataSerializerFactory(new MetadataSerializerFactory())
                    .WithRecordSerializer(new RecordSerializer());
            }

        }
    }
}