using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class StreamAttribute : Attribute
    {
        public StreamAttribute(string category)
        {
            Category = category;
        }
        
        public string Category { get; private set; }
        
    }
}