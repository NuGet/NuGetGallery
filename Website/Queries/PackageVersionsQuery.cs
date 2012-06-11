using System;
using System.Collections.Generic;
using System.Data.Entity;

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
        const string _sqlFormat = @"SELECT p.[Version]
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
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");
            
            var dbContext = (DbContext)_entities;

            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
                prereleaseFilter = "AND p.IsPrerelease = 0";
            return dbContext.Database.SqlQuery<string>(string.Format(_sqlFormat, prereleaseFilter), id);
        }
    }
}