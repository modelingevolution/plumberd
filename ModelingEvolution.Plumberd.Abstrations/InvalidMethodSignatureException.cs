using System;
using System.Reflection;

namespace ModelingEvolution.Plumberd
{
    public class InvalidMethodSignatureException : Exception
    {
        public string MethodName { get; set; }
        public string ClassName { get; set; }

        public InvalidMethodSignatureException()
        {

        }
        public InvalidMethodSignatureException(MethodInfo mth) : this(mth, $"Signature of a method '{mth.Name}' is invalid.") { }
        public InvalidMethodSignatureException(MethodInfo mth, string message) : base(message)
        {
            MethodName = mth.Name;
            ClassName = mth.DeclaringType?.FullName;
        }
    }
}