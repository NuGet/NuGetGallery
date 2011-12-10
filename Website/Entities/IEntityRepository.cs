using System.Linq;

namespace NuGetGallery
{
    public interface IEntityRepository<T>
        where T : class, IEntity, new()
    {
        void CommitChanges();
        void DeleteOnCommit(T entity);
        T Get(int key);
        IQueryable<T> GetAll();
        int InsertOnCommit(T entity);
    }
}