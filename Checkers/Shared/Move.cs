using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventStore;

namespace Checkers.Shared
{
    [Stream("Checkers")]
    public class Move : ICommand
    {
        public int DstRow { get; set; }
        public int DstColumn { get; set; }
        public int SrcRow { get; set; }
        public int SrcColumn { get; set; }
        public Guid Id { get; set; }

        public Move()
        {
            Id = Guid.NewGuid();
        }
    }
}
