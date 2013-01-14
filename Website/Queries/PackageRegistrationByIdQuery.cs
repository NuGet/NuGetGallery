using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageRegistrationByIdQuery
    {
        PackageRegistration Execute(
            string id,
            bool includePackages,
            bool includeOwners = true);
    }

    public class PackageRegistrationByIdQuery : IPackageRegistrationByIdQuery
    {
        private readonly IEntitiesContext _entities;

        public PackageRegistrationByIdQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public PackageRegistration Execute(
            string id,
            bool includePackages,
            bool includeOwners = true)
        {
            IQueryable<PackageRegistration> query = _entities.PackageRegistrations;

            if (includePackages)
            {
                query = query.Include(packageRegistration => packageRegistration.Packages);
            }

            if (includeOwners)
            {
                query = query.Include(packageRegistration => packageRegistration.Owners);
            }

            return query.SingleOrDefault(pr => pr.Id == id);
        }
    }
}