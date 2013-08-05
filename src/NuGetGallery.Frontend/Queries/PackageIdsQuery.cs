using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;

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
        private const string PartialIdSqlFormat = @"SELECT TOP 30 pr.ID
FROM Packages p
    JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID LIKE {{0}}
    {0}
GROUP BY pr.ID
ORDER BY pr.ID";

        private const string NoPartialIdSql = @"SELECT TOP 30 pr.ID
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

            if (String.IsNullOrWhiteSpace(partialId))
            {
                return dbContext.Database.SqlQuery<string>(NoPartialIdSql);
            }

            var prereleaseFilter = String.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = {1}";
            }
            return dbContext.Database.SqlQuery<string>(
                String.Format(CultureInfo.InvariantCulture, PartialIdSqlFormat, prereleaseFilter), partialId + "%", includePrerelease ?? false);
        }
    }
}