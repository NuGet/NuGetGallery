using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public interface ICuratedFeedService
    {
        CuratedFeed GetFeedByName(string name, bool includePackages);
        CuratedFeed GetFeedByKey(int key, bool includePackages);
        IEnumerable<CuratedFeed> GetFeedsForManager(int managerKey);
    }

    public class CuratedFeedService : ICuratedFeedService
    {
        private readonly IEntitiesContext _entities;

        public CuratedFeedService(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public CuratedFeed GetFeedByName(string name, bool includePackages)
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

        public CuratedFeed GetFeedByKey(int key, bool includePackages)
        {
            IQueryable<CuratedFeed> query = _entities.CuratedFeeds;

            if (includePackages)
            {
                query = query
                    .Include(cf => cf.Packages)
                    .Include(cf => cf.Packages.Select(cp => cp.PackageRegistration));
            }

            return query
                .SingleOrDefault(cf => cf.Key == key);
        }

        public IEnumerable<CuratedFeed> GetFeedsForManager(int managerKey)
        {
            return _entities.CuratedFeeds
                .Where(cf => cf.Managers.Any(u => u.Key == managerKey));
        }
    }
}