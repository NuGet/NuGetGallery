using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

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

            var versions = from packageRegistration in _entities.PackageRegistrations
                           where packageRegistration.Id == id
                           from package in packageRegistration.Packages
                           where package.Listed && (includePrerelease == true || !package.IsPrerelease)
                           select package.Version;

            return versions.Select(SemanticVersion.Parse)
                           .OrderByDescending(v => v)
                           .Select(v => v.ToString());
        }
    }
}