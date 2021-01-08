using System;
using System.Collections.Concurrent;
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
                using (MD5 h = MD5.Create())
                {
                    return h.ComputeHash(Encoding.Default.GetBytes(t.FullName));
                }
            });
        }
        public static Guid NameId(this Type t)
        {
            return new Guid(NameHash(t));
        }
    }
}