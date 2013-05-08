using System;
using System.Data;
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set", Justification="This is to match the EF terminology.")]
        DbSet<T> Set<T>() where T : class;
        TResult Sql<TResult>(string query, Func<IDataReader, TResult> reader, int? commandTimeout = null, CommandBehavior behavior = CommandBehavior.Default);
    }
}