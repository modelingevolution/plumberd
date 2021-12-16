using Checkers.Shared;

namespace Checkers.Game;

public sealed class CellViewModel
{
    private readonly int _column;
    private readonly int _row;
    private readonly BoardCell[,] _board;
    public BoardCell State => _board[_column, _row];
    public int Row => _row;
    public int Column => _column;
    public CellViewModel(BoardCell[,] board, int column, int row)
    {
        _column = column;
        _row = row;
        _board = board;
    }
}