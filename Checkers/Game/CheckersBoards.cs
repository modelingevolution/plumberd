using System.Collections.Concurrent;
using Checkers.Shared;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.Metadata;

namespace Checkers.Game;

public class CheckersBoards : IModel
{
    private readonly ConcurrentDictionary<Guid, CheckersBoard> _index;
        
    public CheckersBoards()
    {
        _index = new ConcurrentDictionary<Guid, CheckersBoard>();
            
    }

    public CheckersBoard this[Guid gameId]
    {
        get { return _index.GetOrAdd(gameId, id => new CheckersBoard()); }
    }
    public void Given(IMetadata m, Moved e)
    {
        var board = _index.GetOrAdd(m.StreamId(), (id) => new CheckersBoard());
        board.Moved(e.SrcColumn,e.SrcRow, e.DstColumn, e.DstRow);
            
    }
}