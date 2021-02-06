using System;

namespace ModelingEvolution.Plumberd.Client.GrpcProxy
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EventTypeNameAttribute : Attribute
    {
        public EventTypeNameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}