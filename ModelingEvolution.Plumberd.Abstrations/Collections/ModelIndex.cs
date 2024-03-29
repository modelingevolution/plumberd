﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Collections
{
    public class ModelIndex<TModel> : IEnumerable<KeyValuePair<Guid,TModel>>
        where TModel:new()
    {
        private readonly ConcurrentDictionary<Guid, TModel> _index;
        public void Clear()
        {
            _index.Clear();
        }

        public bool ContainsKey(Guid key)
        {
            return _index.ContainsKey(key);
        }

        public bool TryGetValue(Guid key, out TModel value)
        {
            return _index.TryGetValue(key, out value);
        }

        public bool TryRemove(Guid key, out TModel value)
        {
            return _index.TryRemove(key, out value);
        }

        public int Count => _index.Count;

        public bool IsEmpty => _index.IsEmpty;


        public ModelIndex()
        {
            _index = new ConcurrentDictionary<Guid, TModel>();
        }

        /// <summary>
        /// Always returns an object. Creates new if needed.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public TModel this[Guid id]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _index.GetOrAdd(id, x => new TModel()); }
        }

        public IEnumerator<KeyValuePair<Guid,TModel>> GetEnumerator()
        {
            return _index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _index).GetEnumerator();
        }
    }
    public class ModelIndex<TKey,TModel> : IEnumerable<KeyValuePair<TKey, TModel>>
        where TModel : new()
    {
        private readonly ConcurrentDictionary<TKey, TModel> _index;
        public void Clear()
        {
            _index.Clear();
        }

        public bool ContainsKey(TKey key)
        {
            return _index.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TModel value)
        {
            return _index.TryGetValue(key, out value);
        }

        public bool TryRemove(TKey key, out TModel value)
        {
            return _index.TryRemove(key, out value);
        }

        public int Count => _index.Count;

        public bool IsEmpty => _index.IsEmpty;


        public ModelIndex()
        {
            _index = new ConcurrentDictionary<TKey, TModel>();
        }

        /// <summary>
        /// Always returns an object. Creates new if needed.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public TModel this[TKey id]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _index.GetOrAdd(id, x => new TModel()); }
        }

        public IEnumerator<KeyValuePair<TKey, TModel>> GetEnumerator()
        {
            return _index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_index).GetEnumerator();
        }
    }
}
