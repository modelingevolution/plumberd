using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
using ModelingEvolution.Plumberd.Tests.Models;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ModelingEvolution.Plumberd.Tests
{
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
}