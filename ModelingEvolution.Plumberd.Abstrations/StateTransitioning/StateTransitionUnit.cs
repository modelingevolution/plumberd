using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.StateTransitioning
{
    public abstract class StateTransitionUnit<TUnit, TState> : IStateTransitionUnit
        where TUnit : StateTransitionUnit<TUnit, TState>
        where TState : new()
    {
        static StateTransitionUnit()
        {
            _binder = new StateTransitionBinder<TState>(typeof(TUnit)).Discover();
        }

        private static readonly StateTransitionBinder<TState> _binder;
        public virtual Guid Id
        {
            get => _id;
            set
            {
                _id = value;
                if (_state is IId _stateId)
                {
                    _stateId.Id = value;
                    _state = (TState)_stateId;
                }
            }
        }

        private ulong _version;
        private Guid _id;
        protected TState _state;

        protected StateTransitionUnit()
        {
            _state = new TState();
        }

        public ulong Version
        {
            get { return _version; }
            private set => _version = value;
        }


        public void Rehydrate(IEnumerable<IRecord> events)
        {
            foreach (var i in events.OfType<IEvent>())
            {
                Apply(i);
                _version += 1;
            }
        }

        public async Task RehydrateAsync(IAsyncEnumerable<IRecord> events)
        {
            await foreach (var i in events)
            {
                if (i is IEvent e)
                {
                    Apply(e);
                    _version += 1;
                }
            }
        }

        public IEvent[] Execute(ICommand command)
        {
            return _binder.When(_state, command);
        }

        private void Apply(IEvent @event)
        {
            var given = _binder.Given;
            if (given != null) _state = given(_state, @event);
        }



    }

}