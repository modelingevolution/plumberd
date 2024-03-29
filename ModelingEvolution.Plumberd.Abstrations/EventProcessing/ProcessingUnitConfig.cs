﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.Metadata;
using BindingFlags = ModelingEvolution.Plumberd.Binding.BindingFlags;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    public class ProcessingUnitConfig : IProcessingUnitConfig
    {
        private readonly Type _type;
        private string _name;
        

        public ProcessingUnitConfig(Type type)
        {
            _type = type;
            IsPersistent = false;
            SubscribesFromBeginning = true;
            IsCommandEmitEnabled = true;
            ProcessingMode = ProcessingMode.Both;
            BindingFlags = BindingFlags.ProcessCommands |
                                    BindingFlags.ProcessEvents |
                                    BindingFlags.ReturnCommands |
                                    BindingFlags.ReturnEvents |
                                    BindingFlags.ReturnNothing;

            var att = _type.GetCustomAttribute<ProcessingUnitConfigAttribute>();
            if (att != null)
            {
                SubscribesFromBeginning = att.SubscribesFromBeginning;
                IsEventEmitEnabled = att.IsEventEmitEnabled;
                IsCommandEmitEnabled = att.IsCommandEmitEnabled;
                IsPersistent = att.IsPersistent;
                ProcessingMode = att.ProcessingMode;
                BindingFlags = att.BindingFlags;
                ProcessingLag = att.ProcessingLag;
                RequiresCurrentVersion = att.RequiresCurrentVersion;
                _name = att.StreamName;
            }
        }

        public Type Type => _type;
        public string Name
        {
            get { return _name ?? _type.Name; }
            set { _name = value; }
        }

        public bool SubscribesFromBeginning { get;  set; }
        public bool IsPersistent { get;  set; }
        public bool IsNameOverriden => _name != null;
        public bool IsEventEmitEnabled { get;  set; }
        public bool IsCommandEmitEnabled { get;  set; }
        public bool RequiresCurrentVersion { get; }
        public ProcessingMode ProcessingMode { get;  set; }
        public BindingFlags BindingFlags { get; }
        public TimeSpan ProcessingLag { get; set; }
        public AfterDispatchHandler OnAfterDispatch { get; set; }
        public Action OnLive { get; set; }
        public ProjectionSchema ProjectionSchema { get; set; }
    }
    public delegate Task AfterDispatchHandler(object processingUnit, IMetadata m, IRecord ev, ProcessingResults result);
    
}