using System;
using System.Collections.Generic;
using System.Reflection;
using ModelingEvolution.Plumberd.EventProcessing;

namespace ModelingEvolution.Plumberd.Binding
{
    public interface IEventHandlerBinder
    {
        IEventHandlerBinder Discover(bool searchInProperties, Predicate<MethodInfo> methodFilter = null);
        IEnumerable<Type> Types();
        HandlerDispatcher CreateDispatcher();
    }
}