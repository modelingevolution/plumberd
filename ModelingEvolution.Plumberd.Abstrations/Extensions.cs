using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd
{
    public static class Extensions
    {
        public static Guid SymetricCombine(this Guid x, Guid y)
        {
            if (x.CompareTo(y) > 0)
                return x.Combine(y);
            else return y.Combine(x);
        }
        public static Guid Combine(this Guid x, Guid y)
        {
            byte[] a = x.ToByteArray();
            byte[] b = y.ToByteArray();
            ulong r1 = BitConverter.ToUInt64(a, 0) ^ BitConverter.ToUInt64(b, 8);
            ulong r2 = BitConverter.ToUInt64(a, 8) ^ BitConverter.ToUInt64(b, 0);

            var r = new Span<byte>(new byte[16]);
            BitConverter.TryWriteBytes(r.Slice(0, 8), r1);
            BitConverter.TryWriteBytes(r.Slice(8, 8), r2);
            return new Guid(r);
        }
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> onAdd)
        {
            if (dict.TryGetValue(key, out var value))
                return value;
            dict.Add(key, value = onAdd(key));
            return value;
        }
        public static TValue AddOrUpdate<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> collection, TKey key, Action<TKey, TValue> onUpdate)
            where TValue : new()
        {
            return collection.AddOrUpdate(key, k =>
            {
                var result = new TValue();
                onUpdate(k, result);
                return result;
            }, (k, v) =>
            {
                onUpdate(k, v);
                return v;
            });
        }
       
        public static async Task ExecuteForAll<T>(this IEnumerable<T> list, Func<T, Task> action)
        {
            foreach (var i in list) await action(i);
        }
        public static void ExecuteForAll<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (var i in list) action(i);
        }
        public static bool Contains<T>(this IList<T> list, Predicate<T> searched)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (searched(item))
                    return true;
            }

            return false;
        }
        public static int FirstIndexOf<T>(this IList<T> list, Predicate<T> filter)
        {
            return FirstIndexOf(list, 0, filter);
        }
        public static int FirstIndexOf<T>(this IList<T> list, int start, Predicate<T> filter)
        {
            if (start >= list.Count)
                return -1;
            for (int i = start; i < list.Count; i++)
            {
                var item = list[i];
                if (filter(item))
                    return i;
            }

            return -1;
        }
        public static string LastSegment(this string str, char separator)
        {
            return str.Split(separator, StringSplitOptions.RemoveEmptyEntries).Last();
        }
        public static void RemoveAll<T>(this IList<T> collection, Predicate<T> condition)
        {
            for (int i = 0; i < collection.Count; i++)
            {
                if (condition(collection[i]))
                    collection.RemoveAt(i--);
            }
        }
        public static void InsertSorted<T>(this IList<T> a, T value, IComparer<T> comparer = null)
        {
            var index = a.BinarySearch(value, comparer);
            if (index == a.Count)
                a.Add(value);
            else a.Insert(index, value);
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> other)
        {
            if(list is List<T> l)
                l.AddRange(other);
            else
                foreach (var i in other)
                    list.Add(i);
        }
        public static Int32 BinarySearch<T>(this IList<T> a, T value, IComparer<T> comparer = null)
        {
            Int32 left = 0;
            Int32 right = a.Count - 1;

            var equalityComparer = comparer ?? Comparer<T>.Default;
            Int32 low = left;
            Int32 high = Math.Max(left, right + 1);
            while (low < high)
            {
                Int32 mid = (low + high) / 2;
                if (equalityComparer.Compare(value, a[mid]) <= 0)   // if (value <= a[mid])
                    high = mid;
                else low = mid + 1; // because we compared to a[mid] and the value was larger than a[mid].
                // Thus, the next array element to the right from mid is the next possible
                // candidate for low, and a[mid] can not possibly be that candidate.
            }
            return high;
        }

        
        public static void Merge<TDestination, TSource>(this IList<TDestination> destination,
            IList<TSource> source, Func<TDestination, TSource, bool> match = null,
            Action<TDestination, TSource> onUpdate = null, Func<TDestination> onCreate = null)
        {
            if (onCreate == null)
                onCreate = Activator.CreateInstance<TDestination>;

            if (source == null || source.Count == 0)
            {
                destination.Clear();
                return;
            }
            // Let's add or update
            foreach (var s in source)
            {
                var matched = destination.FirstOrDefault(x => match(x, s));
                if (matched == null) destination.Add(matched = onCreate());

                onUpdate(matched, s);
            }
            // Let's remove
            for (var index = 0; index < destination.Count; index++)
            {
                var r = destination[index];
                if (source.All(x => !match(r, x)))
                    destination.RemoveAt(index--);
            }
        }

        public static string GetLineAfter(this string multiline, string line)
        {
            string[] lines = multiline.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == line)
                {
                    if (i + 1 < lines.Length)
                        return lines[i + 1];
                }
            }

            return lines[0];
        }
        public static TimeSpan ComputeAge(this DateTime? date)
        {
            if (date.HasValue)
                return DateTime.Now.Subtract(date.Value);
            return TimeSpan.MaxValue;
        }
        public static byte[] DownloadFile(this WebClient client, string url)
        {
            return client.OpenRead(url).ReadAllBytes();
        }
        public static byte[] ReadAllBytes(this Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
        public static string GetLine(this string multiline, int number)
        {
            string[] lines = multiline.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines[number];
        }
        public static string FirstLine(this string multiline)
        {
            return multiline.GetLine(0);
        }
        public static string TrimStart(this string s, string start)
        {
            if (s.StartsWith(start))
                return s.Substring(start.Length);
            return s;
        }
    }
}
