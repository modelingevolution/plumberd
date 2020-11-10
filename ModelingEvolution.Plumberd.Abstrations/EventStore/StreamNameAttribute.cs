using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class StreamNameAttribute : Attribute
    {
        public StreamNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}
