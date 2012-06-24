using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageIdsQuery
    {
        IEnumerable<string> Execute(bool? includePrerelease);
    }

    public class PackageIdsQuery : IPackageIdsQuery
    {
        const string _sqlFormat = 
@"SELECT
	pr.ID
FROM Packages p
	JOIN PackageRegistrations pr on pr.[Key] = p.PackageRegistrationKey
WHERE
	p.Listed = 1
    {1}
GROUP BY 
	pr.ID
ORDER BY 
	MAX(pr.DownloadCount) DESC";
        private readonly IEntitiesContext _entities;

        public PackageIdsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public IEnumerable<string> Execute(bool? includePrerelease = false)
        {
            var dbContext = (DbContext)_entities;

            var prereleaseFilter = string.Empty;
            if ((includePrerelease ?? false) == false)
                prereleaseFilter = "AND p.IsPrerelease = 0";
            return dbContext.Database.SqlQuery<string>(string.Format(_sqlFormat, prereleaseFilter));
        }
    }
}