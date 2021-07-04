using System;
using System.Collections.Generic;
using System.Linq;

namespace ModelingEvolution.Plumberd
{
    public interface ICommand : IRecord
    {
    }

    public sealed record CommandMeta 
    {
        public CommandMeta(Guid id)
        {
            Id = id;
            this.Metadata = new Dictionary<string, string>();
        }

        public CommandMeta With(string key, string value)
        {
            Metadata.Add(key, value);
            return this;
        }
        public IDictionary<string, string> Metadata { get; }
        
        public Guid Id { get; init; }
    }
}