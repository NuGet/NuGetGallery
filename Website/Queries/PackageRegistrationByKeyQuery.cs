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
            IQueryable<PackageRegistration> qry = _entities.PackageRegistrations;

            if (includeOwners)
            {
                qry = qry.Include(packageRegistration => packageRegistration.Owners);
            }

            return qry.SingleOrDefault(pr => pr.Key == key);
        }
    }
}