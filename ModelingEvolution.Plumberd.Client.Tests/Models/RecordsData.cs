using System.Collections;
using System.Collections.Generic;

namespace ModelingEvolution.Plumberd.Tests.Models
{
    class RecordsData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new Command2(), true };
            yield return new object[] { new Event1(), false };
            yield return new object[] { new Event2(), false };
            yield return new object[] { new Event3(), false };
            yield return new object[] { new Event4(), false };
            yield return new object[] { new Event5(), false };
            yield return new object[] { new Event6(), false };
            yield return new object[] { new Command1(), true };
            yield return new object[] { new Command3(), false };
            yield return new object[] { new Command4(), false };
            yield return new object[] { new Command5(), false };
            yield return new object[] { new Command6(), false };
            yield return new object[] { new Command7(), false };
            yield return new object[] { new Command8(), false };
            yield return new object[] { new Command9(), false };
            yield return new object[] { new Command10(), false };
            yield return new object[] { new Command11(), false };
            yield return new object[] { new Command12(), false };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}