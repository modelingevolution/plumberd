using System;

namespace ModelingEvolution.Plumberd.Metadata
{
    [Flags]
    public enum ContextScope
    {
        Command = 0x1,
        Event = 0x2,
        Invocation = 0x4,
        All = 0x1 | 0x2 | 0x4
    }
}