using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;

namespace ModelingEvolution.Plumberd
{
    public static class TypeExtensions
    {
        private static ConcurrentDictionary<Type, byte[]> _hashCache = new ConcurrentDictionary<Type, byte[]>();
        public static byte[] NameHash(this Type t)
        {
            return _hashCache.GetOrAdd(t, t =>
            {
                using (SHA256 h = SHA256.Create())
                {
                    var hash = h.ComputeHash(Encoding.Default.GetBytes(t.FullName));

                    ulong n1 = BitConverter.ToUInt64(hash, 0);
                    ulong n2 = BitConverter.ToUInt64(hash, 8);
                    ulong n3 = BitConverter.ToUInt64(hash, 16);
                    ulong n4 = BitConverter.ToUInt64(hash, 24);

                    n1 ^= n3;
                    n2 ^= n4;

                    Memory<byte> m = new Memory<byte>(new byte[16]);
                    BitConverter.TryWriteBytes(m.Span, n1);
                    BitConverter.TryWriteBytes(m.Slice(8,8).Span, n2);
                    return m.ToArray();
                }
                //using (MD5 h = MD5.Create())
                //{
                //    return h.ComputeHash(Encoding.Default.GetBytes(t.FullName));
                //}
            });
        }
        public static Guid NameId(this Type t)
        {
            return new Guid(NameHash(t));
        }
    }
}