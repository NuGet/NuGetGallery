using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IAllPackageRegistrationsQuery
    {
        IEnumerable<PackageRegistration> Execute();
    }

    public class AllPackageRegistrationsQuery : IAllPackageRegistrationsQuery
    {
        private readonly IEntitiesContext _entities;

        public AllPackageRegistrationsQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public IEnumerable<PackageRegistration> Execute()
        {
            return _entities.PackageRegistrations.AsQueryable().Include(x => x.Packages);
        }
    }
}