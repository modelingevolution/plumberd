using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

namespace ModelingEvolution.Plumberd
{
    
    public class TypeRegister
    {
        private readonly Dictionary<Guid, Type> _index;

        public TypeRegister Index(IEnumerable<Type> types)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var i in types.OrderBy(x=>x.Name))
            {
                var id = i.NameId();
                if (!_index.TryGetValue(id, out Type t))
                {
                    sb.Append($"{i.Name}, ");
                    _index.Add(id, i);
                }
            }
            Console.WriteLine($"Contracts found: {sb}");
            return this;
        }

        public TypeRegister Index(params Type[] types)
        {
            return Index((IEnumerable<Type>)types);
        }

        public override string ToString()
        {
            return string.Join(";", _index.Select(x => $"{x.Key}->{x.Value.Name}"));
        }

        public Type this[Guid id]
        {
            get
            {
                if (_index.TryGetValue(id, out var t))
                    return t;

                //string db = ToString();
                return null;
            }
        }
        public Type GetRequiredType(Guid guid)
        {
            if (_index.TryGetValue(guid, out var t))
                return t;
            throw new InvalidResultWhenTryingToGetRequiredType($"There is no type with guid: {guid} in {nameof(TypeRegister)}. Register all necessary types first.");
        }
        public TypeRegister()
        {
            _index = new Dictionary<Guid, Type>();
        }
    }
}