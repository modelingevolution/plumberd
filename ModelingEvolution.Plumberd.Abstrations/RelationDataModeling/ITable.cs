using System;
using System.Linq;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.RelationDataModeling
{
    public interface ITable
    {

    }
    public interface IDbSet<T> : ITable
        where T: class, IRecord
    {
        Task<T> Get(Guid id);
        Task<T> Insert(T record);
        Task<T> Update(T record);
        Task Delete(T record);
        Task Delete(Guid id);
        IQueryable<T> Query();
    }
}