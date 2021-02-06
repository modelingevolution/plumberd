using System;
using Shouldly;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    public class ExtensionTests
    {
        [Fact]
        public void CombineGuidsSymetric()
        {
            Guid a = Guid.NewGuid();
            Guid b = Guid.NewGuid();

            var c1 = a.Combine(b);
            var c2 = b.Combine(a);

            c1.ShouldNotBe(c2);
        }

        [Fact]
        public void CombineGuids()
        {
            Guid a = Guid.NewGuid();
            Guid b = Guid.NewGuid();

            var c = a.Combine(b);
            var c1 = b.Combine(a);

            c1.ShouldNotBe(c);
            c.ShouldNotBe(a);
            c.ShouldNotBe(b);
            c.ShouldNotBe(Guid.Empty);

            var d = c.Combine(b);
            d.ShouldBe(a);
        }
    }
}