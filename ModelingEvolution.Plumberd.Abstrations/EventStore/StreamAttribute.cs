using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    [AttributeUsage(AttributeTargets.Class)]
    public class StreamAttribute : Attribute
    {
        public StreamAttribute(string category, string version = null)
        {
            Category = category;
            Version = version != null ?  Version.Parse(version) : null;
        }
        public Version Version { get; private set; }
        public string Category { get; private set; }
        
    }
}