using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Binding
{
    public static class DynamicHandlerResultFactory
    {
        interface IBuild
        {
            HandlerResultAdapter Create(Type t);
        }
        class TCommand<T> : IBuild where T : ICommand
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<T>(async (m, e) => new ProcessingResults((m.StreamId(), e)));
            }
        }
        class TEvent<T> : IBuild where T:IEvent
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<T>(async (m,e)=>new ProcessingResults((m.StreamId(), e)));
            }
        }
        class Task_TEvent<T> : IBuild where T : IEvent
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<Task<T>>(async (m, e) => new ProcessingResults((m.StreamId(), await e)));
            }
        }
        class Task_TCommand<T> : IBuild where T : ICommand
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<Task<T>>(async (m, e) => new ProcessingResults((m.StreamId(), await e)));
            }
        }
        class Task_Guid_TEvent<T> : IBuild where T : IEvent
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<Task<(Guid, T)>>(async (m, e) => new ProcessingResults(await e));
            }
        }
        class Guid_TEvent<T> : IBuild where T : IEvent
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<(Guid,T)>(async (m, e) => new ProcessingResults(e));
            }
        }
        class Task_Guid_TCommand<T> : IBuild where T : ICommand
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<Task<(Guid, T)>>(async (m, e) => new ProcessingResults(await e));
            }
        }
        class Guid_TCommand<T> : IBuild where T : ICommand
        {
            public HandlerResultAdapter Create(Type t)
            {
                return new HandlerResultAdapter<(Guid, T)>(async (m, e) => new ProcessingResults(e));
            }
        }
        public static HandlerResultAdapter Create_TEvent(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(TEvent<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Task_TEvent(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Task_TEvent<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Task_TCommand(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Task_TCommand<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_TCommand(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(TCommand<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Guid_TEvent(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Guid_TEvent<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Task_Guid_TEvent(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Task_Guid_TEvent<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Guid_TCommand(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Guid_TCommand<>).MakeGenericType(eventType))).Create(eventType);
        }
        public static HandlerResultAdapter Create_Task_Guid_TCommand(Type eventType)
        {
            return ((IBuild)Activator.CreateInstance(typeof(Task_Guid_TCommand<>).MakeGenericType(eventType))).Create(eventType);
        }
    }
}