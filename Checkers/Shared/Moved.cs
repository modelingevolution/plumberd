using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventStore;

namespace Checkers.Shared;

[Stream("Checkers")]
public class Moved : IEvent
{
    public int DstRow { get; set; }
    public int DstColumn { get; set; }
    public int SrcRow { get; set; }
    public int SrcColumn { get; set; }
    public BoardCell Piece { get; set; }
    public Guid Id { get; set; }
    public Moved()
    {
        Id = Guid.NewGuid();
    }
}