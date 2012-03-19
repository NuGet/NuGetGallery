using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface ICuratedFeedByKeyQuery
    {
        CuratedFeed Execute(
            int key,
            bool includePackages = false);
    }

    public class CuratedFeedByKeyQuery : ICuratedFeedByKeyQuery
    {
        private readonly IEntitiesContext _entities;

        public CuratedFeedByKeyQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public CuratedFeed Execute(
            int key,
            bool includePackages = false)
        {
            var qry = _entities.CuratedFeeds.AsQueryable();

            if (includePackages)
            {
                qry = qry
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return qry
                .SingleOrDefault(cf => cf.Key == key);
        }
    }
}