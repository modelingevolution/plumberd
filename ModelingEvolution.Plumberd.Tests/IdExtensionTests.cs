using System;
using FluentAssertions;
using Xunit;
using ModelingEvolution.Plumberd.Binding;

namespace ModelingEvolution.Plumberd.Tests
{
    public class IdExtensionTests
    {
        class FooWithoutId
        {

        }
        class FooWithId
        {
            public Guid Id { get; set; }
        }

        public IdExtensionTests()
        {
            IdExtensions.InitIdAccessor<FooWithId>();
            IdExtensions.InitIdAccessor<FooWithoutId>();
        }

        [Fact]
        public void Id()
        {
            FooWithId x = new FooWithId() { Id = Guid.NewGuid() };

            var id = x.Id();

            id.Should().Be(x.Id);
        }
        [Fact]
        public void NonId()
        {
            FooWithoutId x = new FooWithoutId();

            var id = x.Id();

            id.Should().NotBe(Guid.Empty);
        }

    }
}