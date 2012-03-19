using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Authorize]
    public class CuratedFeedsController : AppController
    {
        public const string Name = "CuratedFeeds";

        [HttpGet]
        public ActionResult CuratedFeed(string name)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute(name, includePackages: true);
            if (curatedFeed == null)
                return HttpNotFound();

            if (!curatedFeed.Managers.Any(manager => manager.Username == Identity.Name))
                return new HttpStatusCodeResult(403);

            return View(new CuratedFeedViewModel
            {
                Name = curatedFeed.Name,
                Managers = curatedFeed.Managers.Select(user => user.Username), 
                IncludedPackages = curatedFeed.Packages
                    .Where(cp => cp.Included)
                    .Select(cp => new CuratedFeedViewModel.IncludedPackage { Id = cp.PackageRegistration.Id, AutomaticallyCurated = cp.AutomaticallyCurated }),
                ExcludedPackages = curatedFeed.Packages
                    .Where(cp => !cp.Included)
                    .Select(cp => cp.PackageRegistration.Id),
            });
        }
    }
}
