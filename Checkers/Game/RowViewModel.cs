using System.Collections;
using Checkers.Shared;

namespace Checkers.Game;

public sealed class RowViewModel : IEnumerable<CellViewModel>
{
    private readonly int _row;
    private readonly BoardCell[,] _board;
    public RowViewModel(BoardCell[,] board, int row)
    {
        _board = board;
        this._row = row;
    }

    public IEnumerator<CellViewModel> GetEnumerator()
    {
        for (int i = 0; i < 8; i++)
        {
            yield return new CellViewModel(_board, i, _row);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}