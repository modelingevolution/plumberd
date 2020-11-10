using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Binding
{
    public static class AsyncEnumerable
    {
        public static async ValueTask<TSource[]> ToArrayAsync<TSource>(this IAsyncEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            await foreach (var i in source)
            {
                result.Add(i);
            }

            return result.ToArray();
        }
      
    }
}