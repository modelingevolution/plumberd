using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using ModelingEvolution.Plumberd.Querying;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class ObservableCollectionViewTests
    {
        class Item : IEquatable<Item>
        {
            public int Id;

            public Item(int id)
            {
                Id = id;
            }

            public bool Equals(Item other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Id == other.Id;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Item)obj);
            }

            public override int GetHashCode()
            {
                return Id;
            }
        }

        class ItemVm : IViewFor<Item>, IEquatable<ItemVm>
        {
            public Item Source { get; set; }

            public bool Equals(ItemVm other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(Source, other.Source);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ItemVm)obj);
            }

            public override int GetHashCode()
            {
                return (Source != null ? Source.GetHashCode() : 0);
            }
        }
        [Fact]
        public void Add()
        {
            ObservableCollection<Item> src = new ObservableCollection<Item>();
            ObservableCollectionView<ItemVm, Item> vm = new ObservableCollectionView<ItemVm, Item>(x => new ItemVm(){Source = x}, src);
            
            src.Add(new Item(1));
            src.Add(new Item(2));

            vm[0].Source.Id.Should().Be(1);
            vm[1].Source.Id.Should().Be(2);
        }
        [Fact]
        public void Replace()
        {
            ObservableCollection<Item> src = new ObservableCollection<Item>() { new Item(1), new Item(2) };
            ObservableCollectionView<ItemVm, Item> vm = new ObservableCollectionView<ItemVm, Item>(x => new ItemVm() { Source = x }, src);

            src[0] = new Item(3);
            
            vm[0].Source.Id.Should().Be(3);
        }
        [Fact]
        public void Insert()
        {
            ObservableCollection<Item> src = new ObservableCollection<Item>();
            ObservableCollectionView<ItemVm, Item> vm = new ObservableCollectionView<ItemVm, Item>(x => new ItemVm(){Source = x}, src);
            
            src.Add(new Item(1));
            src.Add(new Item(3));
            src.Insert(1, new Item(2));

            vm[0].Source.Id.Should().Be(1);
            vm[1].Source.Id.Should().Be(2);
            vm[2].Source.Id.Should().Be(3);
        }
        
        [Fact]
        public void Delete()
        {
            ObservableCollection<Item> src = new ObservableCollection<Item>();
            ObservableCollectionView<ItemVm, Item> vm = new ObservableCollectionView<ItemVm, Item>(x => new ItemVm(){Source = x}, src);
            
            src.Add(new Item(1));
            src.Add(new Item(2));
            src.Add(new Item(3));

            src.RemoveAt(1);
            
            vm[0].Source.Id.Should().Be(1);
            vm[1].Source.Id.Should().Be(3);
        }
    }
}