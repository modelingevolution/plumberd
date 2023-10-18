using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Serialization;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class NativeEventStoreBuilderTests
    {
        
        [Fact]
        public void Build()
        {
           
                ConfigurationBuilder s = new ConfigurationBuilder()
                    .WithMetadataFactory(new MetadataFactory())
                    .WithMetadataSerializerFactory(new MetadataSerializerFactory())
                    .WithRecordSerializer(new RecordSerializer());
           
        }
    }
}