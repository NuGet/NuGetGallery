using System.Data.Entity;

namespace NuGetGallery
{
    public interface IEntitiesContext
    {
        IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        IDbSet<CuratedPackage> CuratedPackages { get; set; }
        IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        IDbSet<User> Users { get; set; }
        int SaveChanges();
        DbSet<T> Set<T>() where T : class;
        void ExecuteSql(string sql, params object[] parameters);
    }
}