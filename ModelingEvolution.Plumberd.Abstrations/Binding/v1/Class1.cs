using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Binding.v1
{
    class ProcessingUnitConfigBuilder
    {
        public ProcessingUnitConfig Config { get; private set; }

        public ProcessingUnitConfigBuilder(Type t, string category)
        {
            Config = new ProcessingUnitConfig(t,category);
        }
        public ProcessingUnitConfigBuilder Register<TArg, TArg2>(Expression<Action<TArg, TArg2>> func)
        {
            return this;
        }
        public ProcessingUnitConfigBuilder Register<TArg>(Expression<Action<TArg>> func)
        {
            return this;
        }
        public ProcessingUnitConfigBuilder Register(MethodInfo m)
        {

            return this;
        }
    }
    class ProcessingUnitConfig
    {
        private readonly List<ProcessingInvocationConfig> _invocations;
        public Type Type { get; private set; }
        public string Category { get; private set; }
        public IEnumerable<ProcessingInvocationConfig> Invocations => _invocations;
        public ProcessingUnitConfig(Type t, string category)
        {
            _invocations = new List<ProcessingInvocationConfig>();
            Type = t;
        }

       
        public ProcessingUnitConfig Register(MethodInfo m, string eventName, InvocationFlow flow, Type recordType)
        {
            if(m == null)
                throw new ArgumentNullException(nameof(m));

            if(string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException(nameof(eventName));

            if (m.DeclaringType != Type)
                throw new ArgumentException(nameof(m));

            var parameters = m.GetParameters();
            if(parameters.Count(x=>x.ParameterType == recordType) != 1)
                throw new ArgumentException(nameof(recordType));

            
            if (_invocations.All(x => x.EventName != eventName))
            {
                _invocations.Add(new ProcessingInvocationConfig(m, eventName, flow, this, recordType));
            }
            return this;
        }
    }
    enum InvocationFlow
    {
        CommandFlow,
        EventFlow
    }
    class ProcessingInvocationConfig
    {
        public ProcessingInvocationConfig(MethodInfo methodInfo, 
            string eventName, 
            InvocationFlow mode, 
            ProcessingUnitConfig processingUnitConfig, 
            Type type)
        {
            MethodInfo = methodInfo;
            EventName = eventName;
            Mode = mode;
            ProcessingUnitConfig = processingUnitConfig;
            Type = type;
        }

        public InvocationFlow Mode { get; private set; }
        public string EventName { get; private set; }
        public ProcessingUnitConfig ProcessingUnitConfig { get; private set; }
        public MethodInfo MethodInfo { get; private set; }
        public Type Type { get; private set; }
    }
    
}
