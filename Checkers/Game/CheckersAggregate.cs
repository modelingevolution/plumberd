using Checkers.Shared;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.StateTransitioning;

namespace Checkers.Game;

[Stream("Checkers")]
public class CheckersAggregate : RootAggregate<CheckersAggregate, CheckersAggregate.State>
{
    public class State
    {
        public readonly BoardCell[,] Board;
        public bool Started;
        private void Setup()
        {
            for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                Board[i, j] = BoardCell.Empty;

            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                Board[i * 2 + j % 2, j] = BoardCell.BlackPawn;

            for (int i = 0; i < 4; i++)
            for (int j = 5; j < 8; j++)
                Board[i * 2 + j % 2, j] = BoardCell.WhitePawn;
        }

        public State()
        {
            Board = new BoardCell[8, 8];
            Setup();
        }
    }

    private static State Given(State current, GameStarted ev)
    {
        current.Started = true;
        return current;
    }
    private static State Given(State current, Moved ev)
    {
        current.Board[ev.SrcColumn, ev.SrcRow] = BoardCell.Empty;
        current.Board[ev.DstColumn, ev.DstRow] = ev.Piece;
        
        return current;
    }
    private static IEnumerable<IEvent> When(State st, Move cmd)
    {
        if (Math.Abs(cmd.DstColumn - cmd.SrcColumn) == 1 && Math.Abs(cmd.DstRow - cmd.SrcRow) == 1)
        {
            var piece = st.Board[cmd.SrcColumn, cmd.SrcRow];
            if (!st.Started)
                yield return new GameStarted();
            yield return new Moved()
            {
                DstColumn = cmd.DstColumn,
                DstRow = cmd.DstRow,
                SrcColumn = cmd.SrcColumn,
                SrcRow = cmd.SrcRow,
                Piece = piece
            };
        }
    }
}