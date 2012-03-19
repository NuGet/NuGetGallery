using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public interface ICuratedFeedsByManagerQuery
    {
        IEnumerable<CuratedFeed> Execute(int managerKey);
    }

    public class CuratedFeedsByManagerQuery : ICuratedFeedsByManagerQuery
    {
        private readonly IEntitiesContext _entities;

        public CuratedFeedsByManagerQuery(IEntitiesContext entities)
        {
            _entities = entities;
        }

        public IEnumerable<CuratedFeed> Execute(int managerKey)
        {
            return _entities.CuratedFeeds
                .Where(cf => cf.Managers.Any(u => u.Key == managerKey));
        }
    }
}