using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EventTypeNameAttribute : Attribute
    {
        public EventTypeNameAttribute(string name)
        {
            Name = name;
        }
        public EventTypeNameAttribute(Type providerType)
        {
            ProviderType = providerType;
        }
        public Type ProviderType { get; private set; }
        public string Name { get; private set; }
    }
}