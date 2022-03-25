﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Querying
{
    public class ObservableCollectionView<TDst, TSrc> :
        IObservableCollectionView<TDst, TSrc>, IDisposable

    where TDst : IViewFor<TSrc>, IEquatable<TDst>
    {
        private readonly Func<TSrc, TDst> _convertItem;
        private readonly IList<TSrc> _internal;
        private readonly ObservableCollection<TDst> _filtered;
        private Predicate<TDst> _filter;
        private static readonly Predicate<TDst> _trueFilter = x => true;


        public Predicate<TDst> Filter
        {
            get { return _filter == _trueFilter ? null : _filter; }
            set
            {
                if (value != null)
                    _filter = value;
                else
                    _filter = _trueFilter;

                Merge();
            }
        }

        private void Merge()
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




        public ObservableCollectionView(Func<TSrc, TDst> convertItem, IList<TSrc> src)
        {
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

        public void CopyTo(Array array, int index)
        {
            ((ICollection)_filtered).CopyTo(array, index);
        }

        public bool IsSynchronized => ((ICollection)_filtered).IsSynchronized;

        public object SyncRoot => ((ICollection)_filtered).SyncRoot;

        public int Add(object value)
        {
            return ((IList)_filtered).Add(value);
        }

        public bool Contains(object value)
        {
            return ((IList)_filtered).Contains(value);
        }

        public int IndexOf(object value)
        {
            return ((IList)_filtered).IndexOf(value);
        }

        public void Insert(int index, object value)
        {
            ((IList)_filtered).Insert(index, value);
        }

        public void Remove(object value)
        {
            ((IList)_filtered).Remove(value);
        }

        public bool IsFixedSize => ((IList)_filtered).IsFixedSize;

        bool IList.IsReadOnly
        {
            get { return false; }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (TDst)value; }
        }


        public void Add(TDst item)
        {
            _filtered.Add(item);
        }

        public void Clear()
        {
            _filtered.Clear();
        }

        public bool Contains(TDst item)
        {
            return _filtered.Contains(item);
        }

        public void CopyTo(TDst[] array, int index)
        {
            _filtered.CopyTo(array, index);
        }

        public IEnumerator<TDst> GetEnumerator()
        {
            foreach (var i in _filtered)
                yield return i;
        }

        public int IndexOf(TDst item)
        {
            return _filtered.IndexOf(item);
        }

        public void Insert(int index, TDst item)
        {
            _filtered.Insert(index, item);
        }

        public bool Remove(TDst item)
        {
            return _filtered.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _filtered.RemoveAt(index);
        }

        public int Count => _filtered.Count;

        bool ICollection<TDst>.IsReadOnly
        {
            get { return false; }
        }

        public TDst this[int index]
        {
            get => _filtered[index];
            set => _filtered[index] = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Move(int oldIndex, int newIndex)
        {
            _filtered.Move(oldIndex, newIndex);
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
