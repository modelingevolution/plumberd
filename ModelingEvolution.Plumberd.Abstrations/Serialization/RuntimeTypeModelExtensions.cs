using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModelingEvolution.Plumberd.EventStore;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ModelingEvolution.Plumberd.Serialization
{
    public static class RuntimeTypeModelExtensions
    {
        public static void RegisterReverseInheritanceFrom(this RuntimeTypeModel model, IEnumerable<Type> types)
        {
            foreach (var i in types)
            {
                var attr = i.GetCustomAttribute<IncludedAsAttribute>();
                if (attr != null)
                {
                    for (var derived = i.BaseType; derived != null && derived != typeof(object); derived = derived.BaseType)
                    {
                        if (derived.GetCustomAttribute<ProtoContractAttribute>() != null)
                        {
                            if (model[derived].GetSubtypes().All(x => x.DerivedType.ConstructType != i && x.FieldNumber != attr.FieldNumber))
                                model[derived].AddSubType(attr.FieldNumber, i);
                        }
                    }
                }
            }
        }
    }
}