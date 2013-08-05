using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;

namespace NuGetGallery
{
    public interface IPackageVersionsQuery
    {
        IEnumerable<string> Execute(
            string id,
            bool? includePrerelease = false);
    }

    public class PackageVersionsQuery : IPackageVersionsQuery
    {
        private const string SqlFormat = @"SELECT p.[Version]
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE pr.ID = {{0}}
	{0}";

        private readonly IEntitiesContext _entities;

        public PackageVersionsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public IEnumerable<string> Execute(
            string id,
            bool? includePrerelease = false)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }

            var dbContext = (DbContext)_entities;

            var prereleaseFilter = String.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = 0";
            }
            return dbContext.Database.SqlQuery<string>(
                String.Format(CultureInfo.InvariantCulture, SqlFormat, prereleaseFilter), id);
        }
    }
}