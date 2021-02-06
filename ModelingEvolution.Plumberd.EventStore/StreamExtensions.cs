using System.IO;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.EventStore
{
    static class StreamExtensions
    {
        public static async Task<int> ReadAllAsync(this Stream s, byte[] buffer, int offset, int count)
        {
            var r = await s.ReadAsync(buffer, offset, count);
            while (r != count)
            {
                var n = await s.ReadAsync(buffer, offset + r, count - r);
                if (n == 0) 
                    return r;
                r += n;
            }
            return r;
        }
    }
}