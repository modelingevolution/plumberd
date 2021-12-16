﻿using Checkers.Shared;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.Metadata;

namespace Checkers.Dashboard;

[ProcessingUnitConfig(IsEventEmitEnabled = false,
    SubscribesFromBeginning = true,
    IsPersistent = false,
    ProcessingMode = ProcessingMode.EventHandler)]
public class DashboardProjection
{
    private readonly DashboardModel _model;

    public DashboardProjection(DashboardModel model)
    {
        _model = model;
    }

    public void Given(IMetadata m, Moved e)
    {
        var gameScore = _model[m.StreamId()];
        switch (e.Piece)
        {
            case BoardCell.WhitePawn:
            case BoardCell.WhiteQueen:
                gameScore.WhiteMovesCount += 1;
                break;
            case BoardCell.BlackPawn:
            case BoardCell.BlackQueen:
                gameScore.BlackMovesCount += 1;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        gameScore.Changed.Execute(e.Id);
    }
}