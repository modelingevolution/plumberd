using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class StreamAttribute : Attribute
    {
        public StreamAttribute(string category)
        {
            Category = category;
        }

        public string Category { get; private set; }
    }
}
