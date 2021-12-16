using System.Collections.Concurrent;

namespace Checkers.Common.Reactive
{
	public static class AsyncHelpers
    {
        /// <summary>
        /// Return typed Task and the specified value.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static Task<T> Return<T>(T value)
        {
            return Task<T>.FromResult(value);
        }

        /// <summary>
        /// Return Task.
        /// </summary>
        public static Task Return()
        {
            return Task<bool>.FromResult(false);
        }

        /// <summary>
        /// Parallelize tasks in partitions limiting the number of operations that are able to run in parallel
        /// like <see cref="Parallel.ForEach"/> do but being an Asynchronous method.
        /// https://blogs.msdn.microsoft.com/pfxteam/2012/03/05/implementing-a-simple-foreachasync-part-2/
        /// </summary>
        /// <returns>The each async.</returns>
        /// <param name="source">Source.</param>
        /// <param name="dop">Degree of parallelism.</param>
        /// <param name="body">Body.</param>
        /// <param name="ct">CancellationToken?, null if not used.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body, CancellationToken ct = default(CancellationToken))
        {
            return Task.WhenAll(
                Partitioner.Create(source).GetPartitions(dop).Select(partition => {
                    return Task.Factory.StartNew(async () => {
                        using (partition)
                        {
                            while (partition.MoveNext())
                            {
                                await body(partition.Current);
                                ct.ThrowIfCancellationRequested();
                            }
                        }
                    }, ct).Unwrap();
                })
            );
        }
    }
}
