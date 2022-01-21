using Checkers.Common.Querying;

namespace Checkers.Dashboard;

public class GameScore
{
    public int WhiteMovesCount { get; set; }
    public int BlackMovesCount { get; set; }
    public WeakEvent<Guid> Changed { get; }

    public GameScore()
    {
        Changed = new WeakEvent<Guid>();
    }
}