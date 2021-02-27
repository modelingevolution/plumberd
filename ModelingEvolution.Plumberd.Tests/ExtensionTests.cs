using System;
using Shouldly;
using Xunit;

namespace ModelingEvolution.Plumberd.Tests
{
    enum x
    {
        none = 0,
        one = 0x1
    }
    public class ExtensionTests
    {
        [Fact]
        public void GuidCombineWithEnum()
        {
            Guid g = Guid.Parse("0D12CD4D-F333-4A1B-84FC-96A727D0A79F");
            Guid c = Guid.Parse("0D12CD4D-F333-4A1B-85FC-96A727D0A79F");

            g.Combine(x.none).ShouldBe(g);

            g.Combine(x.one).ShouldBe(c);
        }

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