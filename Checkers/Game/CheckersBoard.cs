using Checkers.Common.Querying;
using Checkers.Shared;

namespace Checkers.Game;

public class CheckersBoard
{
    private readonly BoardCell[,] _board;
    public WeakEvent<string> Changed { get; }
    public IEnumerable<RowViewModel> Rows()
    {
        for (int i = 0; i < 8; i++)
            yield return new RowViewModel(_board, i);
    }

    public void Moved(int srcColumn, int srcRow, int dstColumn, int dstRow)
    {
        var src = this[srcColumn, srcRow];
        this[dstColumn, dstRow] = src;
        this[srcColumn, srcRow] = BoardCell.Empty;
        string move = $"{(char)('A' + srcColumn)}{srcRow} to {(char)('A' + dstColumn)}{dstRow}";
        Changed.Execute(move);
    }
    
    public BoardCell this[int column, int row] { get { return _board[column, row]; }
        set => _board[column, row] = value;
    }
    public CheckersBoard()
    {
        _board = new BoardCell[8, 8];
        Changed = new WeakEvent<string>();
        Setup();
    }

        
    private void Setup()
    {
        for (int i = 0; i < 8; i++)
        for (int j = 0; j < 8; j++)
            _board[i, j] = BoardCell.Empty;

        for (int i = 0; i < 4; i++)
        for (int j = 0; j < 3; j++)
            _board[i * 2 + j % 2, j] = BoardCell.BlackPawn;

        for (int i = 0; i < 4; i++)
        for (int j = 5; j < 8; j++)
            _board[i * 2 + j % 2, j] = BoardCell.WhitePawn;
    }

}