using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventProcessing;

namespace Checkers.Game
{
    [ProcessingUnitConfig(IsEventEmitEnabled = false,
        SubscribesFromBeginning = true,
        IsPersistent = false,
        ProcessingMode = ProcessingMode.EventHandler)]
    public class CheckersBoardProjection
    {
        public CheckersBoardProjection(CheckersBoards boards)
        {
            Boards = boards;
        }

        public CheckersBoards Boards { get; }
    }
}