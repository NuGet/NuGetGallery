using System.Collections.Generic;
using System.Data.Entity;

namespace NuGetGallery
{
    public interface IPackageIdsQuery
    {
        IEnumerable<string> Execute(
            string partialId,
            bool? includePrerelease = false);
    }

    public class PackageIdsQuery : IPackageIdsQuery
    {
        const string _partialIdSqlFormat = @"SELECT TOP 30 pr.ID
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID LIKE {{0}}
	{0}
GROUP BY pr.ID
ORDER BY pr.ID";
        private const string _noPartialIdSql = @"SELECT TOP 30 pr.ID
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
GROUP BY pr.ID
ORDER BY MAX(pr.DownloadCount) DESC";
        private readonly IEntitiesContext _entities;

        public PackageIdsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public IEnumerable<string> Execute(
            string partialId,
            bool? includePrerelease = false)
        {
            var dbContext = (DbContext)_entities;

            if (string.IsNullOrWhiteSpace(partialId))
                return dbContext.Database.SqlQuery<string>(_noPartialIdSql);
            
            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
                prereleaseFilter = "AND p.IsPrerelease = {1}";
            return dbContext.Database.SqlQuery<string>(string.Format(_partialIdSqlFormat, prereleaseFilter), partialId + "%", includePrerelease ?? false);
        }
    }
}