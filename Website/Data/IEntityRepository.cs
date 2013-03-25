using System.Linq;

namespace NuGetGallery.Data
{
    public interface IEntityRepository<T>
        where T : class, IEntity, new()
    {
        void CommitChanges();
        void DeleteOnCommit(T entity);
        T GetEntity(int key);
        IQueryable<T> GetAll();
        int InsertOnCommit(T entity);
    }
}