using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using static System.Formats.Asn1.AsnWriter;


namespace ModelingEvolution.Plumberd.Querying
{

    public interface IObservableCollectionView<TDst, TSrc> : INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<TDst>
        where TDst : IViewFor<TSrc>, IEquatable<TDst>
    {

    }
    
    public class ObservableCollectionView<T> : INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<T>, IDisposable
    {
        class LockedEnumerator : IEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;
            private readonly ReaderWriterLockSlim _lock;

            public LockedEnumerator(IEnumerable<T> src, ReaderWriterLockSlim l)
            {
                _lock = l;
                _lock.EnterReadLock();
                _inner = src.GetEnumerator();
            }
            public bool MoveNext()
            {
                return _inner.MoveNext();
            }

            public void Reset()
            {
                _inner.Reset();
            }

            public T Current => _inner.Current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _lock.ExitReadLock();
            }
        }
        private readonly IReadOnlyList<T> _internal;
        private readonly ObservableCollection<T> _filtered;
        private Predicate<T> _filter;
        private readonly ReaderWriterLockSlim _lock;
        private readonly ReaderWriterLockSlimScope _scope;
        public IReaderWriterLockSlimScope Lock => _scope;
        public Predicate<T> Filter
        {
            get { return _filter; }
            set
            {
                if (value != null)
                    _filter = value;
                else
                    _filter = x => true;

                Merge();
            }
        }

        private void Merge()
        {
            _lock.EnterWriteLock();
            try
            {
                int index = 0;
                foreach (var src in _internal)
                {
                    if (index < _filtered.Count)
                    {
                        if (_filter(src))
                        {
                            // we expect the same object in dst
                            if (!object.ReferenceEquals(src, _filtered[index]))
                            {
                                _filtered.Insert(index, src);
                            }

                            index += 1;
                            continue;
                        }
                        else
                        {
                            if (object.ReferenceEquals(src, _filtered[index]))
                            {
                                _filtered.RemoveAt(index);
                            }

                            continue;
                        }
                    }
                    else
                    {
                        if (_filter(src))
                        {
                            _filtered.Add(src);
                            index += 1;
                        }
                    }
                }

                while (index < _filtered.Count)
                    _filtered.RemoveAt(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Refresh()
        {
            Merge();
        }
        public IReadOnlyList<T> Source => _internal;
        public int Count => _filtered.Count;
        public ObservableCollectionView(IReadOnlyList<T> src = null)
        {
            _lock = new ReaderWriterLockSlim();
            _scope = new ReaderWriterLockSlimScope(_lock);
            _internal = src ?? new ObservableCollection<T>();
            _filtered = new ObservableCollection<T>();
            _filtered.AddRange(_internal);

            if (_internal is INotifyCollectionChanged nch)
                nch.CollectionChanged += SourceCollectionChanged;
            else throw new ArgumentException("Source collection must implement INotifyCollectionChanged");

            _filtered.CollectionChanged += ViewCollectionChanged;
            ((INotifyPropertyChanged)_filtered).PropertyChanged += ViewPropertyChanged;
            _filter = x => true;
        }

        
        private void ViewPropertyChanged(object s, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            PropertyChanged?.Invoke(this, propertyChangedEventArgs);
        }

        private void ViewCollectionChanged(object s, NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
        }

        private void SourceCollectionChanged(object s, NotifyCollectionChangedEventArgs args)
        {
            _lock.EnterWriteLock();
            try
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    var toAdd = args.NewItems.OfType<T>().Where(x => _filter(x)).ToArray();
                    _filtered.AddRange(toAdd);
                }
                else if (args.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (var i in args.OldItems.OfType<T>().Where(x => _filter(x)))
                    {
                        _filtered.Remove(i);
                    }
                }
                else if (args.Action == NotifyCollectionChangedAction.Replace)
                {
                    for (int i = 0; i < args.NewItems.Count; i++)
                    {
                        var item = (T)args.NewItems[i];
                        var index = _filtered.IndexOf(item);
                        if (index >= 0)
                            _filtered[index] = item;
                    }

                }
                else if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    _filtered.Clear();
                    _filtered.AddRange(_internal.Where(x => _filter(x)));
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        public void CopyTo(Array array, int index)
        {
            _lock.EnterReadLock();
            try
            {

                ((ICollection)_filtered).CopyTo(array, index);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool IsSynchronized => ((ICollection)_filtered).IsSynchronized;

        public object SyncRoot => ((ICollection)_filtered).SyncRoot;

        //public int Add(object value)
        //{
        //    return ((IList)_filtered).Add(value);
        //}

        //public bool Contains(object value)
        //{
        //    return ((IList)_filtered).Contains(value);
        //}

        public int IndexOf(object value)
        {
            _lock.EnterReadLock();
            try
            {
                return ((IList)_filtered).IndexOf(value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

       
        public bool IsFixedSize => ((IList)_filtered).IsFixedSize;

       

        public bool Contains(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return _filtered.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CopyTo(T[] array, int index)
        {
            _lock.EnterReadLock();
            try
            {
                _filtered.CopyTo(array, index);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new LockedEnumerator(this._filtered, _lock);
        }

        public int IndexOf(T item)
        {
            _lock.EnterReadLock();
            try
            {
                return _filtered.IndexOf(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        

        public T this[int index]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _filtered[index];
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Move(int oldIndex, int newIndex)
        {
            _lock.EnterWriteLock();
            try
            {
                _filtered.Move(oldIndex, newIndex);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            if (_internal is INotifyCollectionChanged nch)
                nch.CollectionChanged -= SourceCollectionChanged;
        }
    }
}