using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public interface IManageFeedService
    {
        Feed GetFeedByName(string feedName);
        IEnumerable<Feed> GetFeedsForManager(int managerKey);

        void CreateFeedRule(Feed feed, PackageRegistration packageRegistration, string packageVersionSpec, string notes);
    }
}