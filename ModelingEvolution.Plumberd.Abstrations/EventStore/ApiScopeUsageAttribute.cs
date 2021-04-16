using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModelingEvolution.Plumberd.EventStore
{
    public static class TypeEnumerableScopeExtensions
    {
        public static IEnumerable<Type> MinScopeRequired(this IEnumerable<Type> types, ApiScope required)
        {
            int r = (int) required;
            return types.Where(x => ((int)(x.GetCustomAttribute<ApiScopeUsageAttribute>()?.Scope ?? (x.IsPublic ? ApiScope.Public : ApiScope.Private))) >= r);
        }
    }
    [AttributeUsage(AttributeTargets.Class)]
    public class ApiScopeUsageAttribute : Attribute
    {
        public readonly ApiScope Scope;

        public ApiScopeUsageAttribute(ApiScope scope)
        {
            Scope = scope;
        }
    }
}
