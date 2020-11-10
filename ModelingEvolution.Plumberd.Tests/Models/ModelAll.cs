using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.Tests.Models
{
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
}