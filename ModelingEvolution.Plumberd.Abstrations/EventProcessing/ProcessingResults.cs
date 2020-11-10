using System;
using System.Collections.Generic;
using System.Linq;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventProcessing
{
    public struct ProcessingResults
    {
        public bool IsEmpty
        {
            get => (_commands == null || _commands.Count == 0) && (_events == null || _events.Count == 0);
        }
        public static ProcessingResults Empty = new ProcessingResults();

        public ProcessingResults((Guid, ICommand)[] commands)
        {
            _commands = new List<(Guid, ICommand)>(commands);
            _events = null;
        }
        public ProcessingResults((Guid, ICommand) cmd)
        {
            _commands = new List<(Guid, ICommand)>() { cmd };
            _events = null;
        }
        public ProcessingResults((Guid, IEvent)[] events)
        {
            _commands = null;
            _events = new List<(Guid, IEvent)>(events);
        }
        public ProcessingResults((Guid, IEvent) ev)
        {
            _commands = null;
            _events = new List<(Guid, IEvent)>() { ev };
        }

        public ProcessingResults(IEnumerable<(Guid, ICommand)> commands)
        {
            _commands = new List<(Guid, ICommand)>(commands);
            _events = null;
        }
        public ProcessingResults(IEnumerable<(Guid, IEvent)> events)
        {
            _commands = null;
            _events = new List<(Guid, IEvent)>(events);
        }
        public static ProcessingResults operator +(ProcessingResults left, ProcessingResults right)
        {
            if (right._events != null)
            {
                if (left._events == null)
                    left._events = new List<(Guid, IEvent)>();
                left._events.AddRange(right._events);
            }

            if (right._commands != null)
            {
                if (left._commands == null)
                    left._commands = new List<(Guid, ICommand)>();
                left._commands.AddRange(right._commands);
            }
            return left;
        }

        public IEnumerable<(Guid, ICommand)> Commands => _commands ?? Array.Empty<(Guid, ICommand)>().AsEnumerable();
        public IEnumerable<(Guid, IEvent)> Events => _events ?? Array.Empty<(Guid, IEvent)>().AsEnumerable();
        private List<(Guid, ICommand)> _commands;
        private List<(Guid, IEvent)> _events;
    }
}