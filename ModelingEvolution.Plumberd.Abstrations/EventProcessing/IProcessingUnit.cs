﻿using System;
using System.Diagnostics;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    
    public interface IProcessingUnit : IDisposable
    {
        IEventStore EventStore { get; }
        IProcessingUnitConfig Config { get; }
        ICommandInvoker CommandInvoker { get; }
        ProcessingMode ProcessingMode { get; }

    }
}