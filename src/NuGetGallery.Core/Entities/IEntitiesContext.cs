using System.Data.Entity;

namespace NuGetGallery
{
    public interface IEntitiesContext
    {
        IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        IDbSet<CuratedPackage> CuratedPackages { get; set; }
        IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        IDbSet<Credential> Credentials { get; set; }
        IDbSet<User> Users { get; set; }
        int SaveChanges();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set", Justification="This is to match the EF terminology.")]
        IDbSet<T> Set<T>() where T : class;
        void DeleteOnCommit<T>(T entity) where T : class;
    }
}