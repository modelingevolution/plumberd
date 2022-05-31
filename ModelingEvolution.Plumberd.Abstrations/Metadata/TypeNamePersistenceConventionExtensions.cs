using System;

namespace ModelingEvolution.Plumberd.Metadata;

public static class TypeNamePersistenceConventionExtensions
{
    public static Func<Type, string> GetConverter(this TypeNamePersistenceConvention convention)
    {
        switch (convention)
        {
            case TypeNamePersistenceConvention.AssemblyQualifiedName:
                return x => x.AssemblyQualifiedName;
            case TypeNamePersistenceConvention.FullName:
                return x => x.FullName;
            case TypeNamePersistenceConvention.Name:
                return x => x.Name;
            default:
                throw new ArgumentOutOfRangeException(nameof(convention), convention, null);
        }
    }
}