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
            IQueryable<CuratedFeed> qry = _entities.CuratedFeeds;

            if (includePackages)
            {
                qry = qry
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return qry
                .SingleOrDefault(cf => cf.Name == name);
        }
    }
}