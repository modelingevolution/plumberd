using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ModelingEvolution.Plumberd.Querying
{
    public interface IReaderWriterLockSlimScope
    {
        IDisposable LockForWrite();
        IDisposable LockForRead();
    }

    class ReaderWriterLockSlimScope : IReaderWriterLockSlimScope
    {
        private readonly ReaderWriterLockSlim _lock;

        public ReaderWriterLockSlimScope(ReaderWriterLockSlim @lock)
        {
            _lock = @lock;
        }

        private readonly struct DisposableAction : IDisposable
        {
            private readonly Action<ReaderWriterLockSlim> _action;
            private readonly ReaderWriterLockSlim _lock;
            public DisposableAction(Action<ReaderWriterLockSlim> a, ReaderWriterLockSlim @lock)
            {
                _action = a;
                _lock = @lock;
            }
            public void Dispose()
            {
                _action(_lock);
            }
        }

        public IDisposable LockForWrite()
        {
            _lock.EnterWriteLock();
            return new DisposableAction((x) => x.ExitWriteLock(), _lock);
        }
        public IDisposable LockForRead()
        {
            _lock.EnterReadLock();
            return new DisposableAction((x) => x.ExitReadLock(), _lock);
        }
    }
    public class ObservableCollectionView<TDst, TSrc> :
        IObservableCollectionView<TDst, TSrc>, IDisposable

    where TDst : IViewFor<TSrc>, IEquatable<TDst>
    {
        class LockedEnumerator : IEnumerator<TDst>
        {
            private readonly IEnumerator<TDst> _inner;
            private readonly ReaderWriterLockSlim _lock;

            public LockedEnumerator(IEnumerable<TDst> src, ReaderWriterLockSlim l)
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

            public TDst Current => _inner.Current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _lock.ExitReadLock();
            }
        }
        private readonly Func<TSrc, TDst> _convertItem;
        private readonly IReadOnlyList<TSrc> _internal;
        private readonly ObservableCollection<TDst> _filtered;
        private Predicate<TDst> _filter;
        private static readonly Predicate<TDst> _trueFilter = x => true;
        private readonly ReaderWriterLockSlim _lock;
        private readonly ReaderWriterLockSlimScope _scope;
        public IReaderWriterLockSlimScope Lock => _scope;
        public Predicate<TDst> Filter
        {
            get { return _filter == _trueFilter ? null : _filter; }
            set
            {
                _filter = value ?? _trueFilter;

                Merge();
            }
        }
        public void Refresh()
        {
            Merge();
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
                        TDst c;
                        if (_filter(c = _convertItem(src)))
                        {
                            // we expect the same object in dst
                            if (!object.ReferenceEquals(src, _filtered[index].Source))
                            {
                                _filtered.Insert(index, c);
                            }

                            index += 1;
                            continue;
                        }
                        else
                        {
                            if (object.ReferenceEquals(src, _filtered[index].Source))
                            {
                                _filtered.RemoveAt(index);
                            }
                            continue;
                        }
                    }
                    else
                    {
                        TDst c;
                        if (_filter(c = _convertItem(src)))
                        {
                            _filtered.Add(c);
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





        public ObservableCollectionView(Func<TSrc, TDst> convertItem, IReadOnlyList<TSrc> src)
        {
            _lock = new ReaderWriterLockSlim();
            _scope = new ReaderWriterLockSlimScope(_lock);
            _convertItem = convertItem;
            _internal = src;
            _filtered = new ObservableCollection<TDst>();
            _filtered.AddRange(_internal.Select(_convertItem));

            var srcCollectionChanges = src as INotifyCollectionChanged;
            if (srcCollectionChanges == null)
                throw new ArgumentException("src must implement INotifyCollectionChanged");
            srcCollectionChanges.CollectionChanged += OnSrcCollectionChangesOnCollectionChanged;
            _filtered.CollectionChanged += (s, e) => ViewCollectionChanged(e);
            ((INotifyPropertyChanged)_filtered).PropertyChanged += (s, e) => ViewPropertyChanged(e);
            _filter = _trueFilter;
        }

        private void OnSrcCollectionChangesOnCollectionChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            SourceCollectionChanged(e);
        }

        private void ViewPropertyChanged(PropertyChangedEventArgs propertyChangedEventArgs)
        {
            PropertyChanged?.Invoke(this, propertyChangedEventArgs);
        }

        private void ViewCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
        }

        public bool IsFiltered => Filter != null;
        private void SourceCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            _lock.EnterWriteLock();
            try
            {

                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    var toAdd = args.NewItems.OfType<TSrc>()
                        .Select(_convertItem)
                        .Where(x => _filter(x))
                        .ToArray();
                    if (!IsFiltered)
                    {
                        // we care about the order
                        if (args.NewStartingIndex == _filtered.Count)
                        {
                            // we're adding at the end
                            _filtered.AddRange(toAdd);
                        }
                        else
                        {
                            // it's in the middle
                            foreach (var i in toAdd.Reverse())
                            {
                                _filtered.Insert(args.NewStartingIndex, i);
                            }
                        }
                    }
                    else
                        _filtered.AddRange(toAdd);
                }
                else if (args.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (var i in args.OldItems.OfType<TSrc>()
                        .Select(_convertItem)
                        .Where(x => _filter(x)))
                    {
                        _filtered.Remove(i);
                    }
                }
                else if (args.Action == NotifyCollectionChangedAction.Replace)
                {
                    if (!IsFiltered)
                    {
                        for (int i = 0; i < args.NewItems.Count; i++)
                        {
                            _filtered[i + args.OldStartingIndex] = _convertItem((TSrc)args.NewItems[i]);
                        }
                    }
                    else throw new NotSupportedException();
                }
                else if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    _filtered.Clear();
                    _filtered.AddRange(_internal.Select(_convertItem));
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


        public bool Contains(object value)
        {
            _lock.EnterReadLock();
            try
            {
                return ((IList)_filtered).Contains(value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

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


        public bool Contains(TDst item)
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

        public void CopyTo(TDst[] array, int index)
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

        public IEnumerator<TDst> GetEnumerator()
        {
            return new LockedEnumerator(this._filtered, _lock);
        }

        public int IndexOf(TDst item)
        {
            return _filtered.IndexOf(item);
        }

        public void Insert(int index, TDst item)
        {
            _lock.EnterReadLock();
            try
            {
                _filtered.Insert(index, item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }


        public int Count => _filtered.Count;



        public TDst this[int index]
        {
            get
            {
                return _filtered[index];
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
            ((INotifyCollectionChanged)_internal).CollectionChanged -= OnSrcCollectionChangesOnCollectionChanged;
        }
    }
}
