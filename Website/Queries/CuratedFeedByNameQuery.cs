using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface ICuratedFeedByNameQuery
    {
        CuratedFeed Execute(
            string name,
            bool includePackages);
    }

    public class CuratedFeedByNameQuery : ICuratedFeedByNameQuery
    {
        private readonly IEntitiesContext _entities;

        public CuratedFeedByNameQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public CuratedFeed Execute(
            string name,
            bool includePackages)
        {
            IQueryable<CuratedFeed> query = _entities.CuratedFeeds;

            if (includePackages)
            {
                query = query
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return query
                .SingleOrDefault(cf => cf.Name == name);
        }
    }
}