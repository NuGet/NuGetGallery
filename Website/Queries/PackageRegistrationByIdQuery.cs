using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IPackageRegistrationByIdQuery
    {
        PackageRegistration Execute(
            string id,
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
            bool includeOwners = true)
        {
            var qry = _entities.PackageRegistrations.AsQueryable();

            if (includeOwners)
                qry = qry.Include(packageRegistration => packageRegistration.Owners);

            return qry.SingleOrDefault(pr => pr.Id == id);
        }
    }
}