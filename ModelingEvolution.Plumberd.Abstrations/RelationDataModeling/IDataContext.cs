using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.RelationDataModeling
{
    public interface IDbProvider
    {

    }
    public interface IUnitOfWork
    {
        IDbSet<T> Set<T>() where T:class, IRecord;
        Task Commit();
    }
}