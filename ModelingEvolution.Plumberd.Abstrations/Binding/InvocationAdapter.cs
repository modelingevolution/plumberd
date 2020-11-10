using ModelingEvolution.Plumberd.EventProcessing;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Binding
{
    public class InvocationAdapter<TArg0, TArg1, TArg2> : IInvocationAdapter
    {
        private static HandlerDispatcher Build(HandlerParameterAdapter<TArg0, TArg1, TArg2> paramAdapter,
            HandlerResultAdapterEmpty resultAdapter)
        {
            return async (unit, metadata, record) =>
            {
                paramAdapter.Invoke(unit, metadata, record);
                return resultAdapter.Empty;
            };
        }

        public HandlerDispatcher Build(HandlerParameterAdapter parameterAdapter, HandlerResultAdapter resultAdapter)
        {
            var handlerResultAdapter = (HandlerResultAdapterEmpty)resultAdapter;
            var handlerParameterAdapter = (HandlerParameterAdapter<TArg0, TArg1, TArg2>)parameterAdapter;
            return Build(handlerParameterAdapter,
                handlerResultAdapter);
        }
    }
    public class InvocationAdapter<TArg0, TArg1, TArg2, TResult> : IInvocationAdapter
    {
        private static HandlerDispatcher Build(HandlerParameterAdapter<TArg0, TArg1, TArg2, TResult> paramAdapter,
            HandlerResultAdapter<TResult> resultAdapter)
        {
            return async (unit, metadata, record) => await resultAdapter.Returns(metadata, paramAdapter.Convert(unit, metadata, record));
        }

        public HandlerDispatcher Build(HandlerParameterAdapter parameterAdapter, HandlerResultAdapter resultAdapter)
        {
            var handlerResultAdapter = (HandlerResultAdapter<TResult>)resultAdapter;
            var handlerParameterAdapter = (HandlerParameterAdapter<TArg0, TArg1, TArg2, TResult>)parameterAdapter;
            return Build(handlerParameterAdapter,
                handlerResultAdapter);
        }
    }
}