using System.Collections.Generic;
using ModelingEvolution.Plumberd.StateTransitioning;

namespace ModelingEvolution.Plumberd.Tests.Models
{
    public class ComplexTransitionUnit : StateTransitionUnit<ComplexTransitionUnit, ComplexTransitionUnit.State>
    {
        public struct State
        {
            public string Name { get; set; }
        }

        private static Event2 When(State st, Command2 cmd)
        {
            return new Event2();
        }


        private static IEnumerable<IEvent> When(State st, Command1 cmd)
        {
            yield return new Event1();
        }

        public State GetState() => _state;
        public static State Given(State st, Event1 e)
        {
            st.Name = "Foo";
            return st;
        }

    }
}