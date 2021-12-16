using ModelingEvolution.Plumberd;

namespace Checkers.Dashboard;

public class DashboardModel : IModel
{
    private readonly Dictionary<Guid, GameScore> _index;

    public DashboardModel()
    {
        _index = new Dictionary<Guid, GameScore>();
    }
    public GameScore this[Guid gameId]
    {
        get { return _index.GetOrAdd(gameId, id => new GameScore()); }
    }
}