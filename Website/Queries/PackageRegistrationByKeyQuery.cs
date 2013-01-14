using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageRegistrationByKeyQuery
    {
        PackageRegistration Execute(
            int key,
            bool includeOwners = true);
    }

    public class PackageRegistrationByKeyQuery : IPackageRegistrationByKeyQuery
    {
        private readonly IEntitiesContext _entities;

        public PackageRegistrationByKeyQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public PackageRegistration Execute(
            int key,
            bool includeOwners = true)
        {
            IQueryable<PackageRegistration> query = _entities.PackageRegistrations;

            if (includeOwners)
            {
                query = query.Include(packageRegistration => packageRegistration.Owners);
            }

            return query.SingleOrDefault(pr => pr.Key == key);
        }
    }
}