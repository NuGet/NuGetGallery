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
        private readonly EntitiesContext _dbContext;

        public CuratedFeedsByManagerQuery(EntitiesContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IEnumerable<CuratedFeed> Execute(int managerKey)
        {
            return _dbContext.CuratedFeeds
                .Where(cf => cf.Managers.Any(u => u.Key == managerKey));
        }
    }
}