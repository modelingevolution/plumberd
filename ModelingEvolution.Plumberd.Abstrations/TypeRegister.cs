using System;
using System.Collections.Generic;
using System.Linq;

namespace ModelingEvolution.Plumberd
{
    
    public class TypeRegister
    {
        private readonly Dictionary<Guid, Type> _index;

        public TypeRegister Index(IEnumerable<Type> types)
        {
            foreach (var i in types)
            {
                var id = i.NameId();
                if (!_index.TryGetValue(id, out Type t))
                    _index.Add(id, i);
            }

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
        public TypeRegister()
        {
            _index = new Dictionary<Guid, Type>();
        }
    }
}