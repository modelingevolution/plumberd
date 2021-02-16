using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public class AggregateRepositoryCacheDecorator<TAggregate> : IAggregateRepository<TAggregate>
        where TAggregate : IRootAggregate, new()
    {
        private readonly IAggregateRepository<TAggregate> _next;
        private readonly IMemoryCache _cache;
        public AggregateRepositoryCacheDecorator(IAggregateRepository<TAggregate> next, IMemoryCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task<TAggregate> Get(Guid id)
        {
            var key = (typeof(TAggregate), id);
            if (_cache.TryGetValue(key, out var aggregate))
                return (TAggregate)aggregate;

            var result = await _next.Get(id);

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(30));
            _cache.Set(key, result, cacheEntryOptions);

            return result;
        }

        public Task<IRecord[]> GetEvents(Guid id)
        {
            return _next.GetEvents(id);
        }
    }
}