﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.StateTransitioning;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Tests.Models
{
    public class ComplexTransitionUnit : RootAggregate<ComplexTransitionUnit, ComplexTransitionUnit.State>
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
    [ProcessingUnitConfig(IsEventEmitEnabled = true, 
        IsCommandEmitEnabled = false,
        IsPersistent = true,
        ProcessingMode = ProcessingMode.CommandHandler)]

    public class SimpleCommandHandler
    {
        public SimpleEvent When(Guid g, SimpleCommand c)
        {
            return new SimpleEvent();
        }
    }
    [ProcessingUnitConfig(IsEventEmitEnabled = true,
        IsCommandEmitEnabled = false,
        IsPersistent = true,
        ProcessingMode = ProcessingMode.CommandHandler)]
    public class CommandHandlerWithExceptions
    {
        public async Task<IEvent> When(Guid id, CommandRaisingException cmd)
        {
            throw new ProcessingException<MyExceptionData>(new MyExceptionData(){ Text = "Yes, yes yes!" });
        }
    }
}