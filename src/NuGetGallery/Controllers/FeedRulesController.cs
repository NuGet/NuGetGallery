using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Authorize]
    public partial class FeedRulesController : AppController
    {
        internal IManageFeedService ManageFeedService { get; set; }
        internal IEntitiesContext EntitiesContext { get; set; }

        protected FeedRulesController() { }

        public FeedRulesController(
            IManageFeedService manageFeedService,
            IEntitiesContext entitiesContext)
        {
            ManageFeedService = manageFeedService;
            EntitiesContext = entitiesContext;
        }

        [ActionName("FeedRulesForm")]
        [HttpGet]
        public virtual ActionResult GetFeedRulesForm(string feedName)
        {
            
            var feed = ManageFeedService.GetFeedByName(feedName);
            if (feed == null)
            {
                return HttpNotFound();
            }

            if (feed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            ViewBag.FeedName = feed.Name;
            return View();
        }

        [ActionName("FeedRules")]
        [HttpPost]
        public virtual ActionResult PostCuratedPackages(
            string feedName,
            CreateFeedRuleRequest request)
        {
            Feed feed = ManageFeedService.GetFeedByName(feedName);
            if (feed == null)
            {
                return HttpNotFound();
            }

            if (feed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.FeedName = feed.Name;
                return View("FeedRulesForm");
            }

            PackageRegistration packageRegistration = EntitiesContext.PackageRegistrations
                .Where(pr => pr.Id == request.PackageId)
                .FirstOrDefault();

            if (packageRegistration == null)
            {
                ModelState.AddModelError("PackageId", Strings.PackageWithIdDoesNotExist);
                ViewBag.FeedName = feed.Name;
                return View("FeedRulesForm");
            }

            ManageFeedService.CreateFeedRule(
                feed,
                packageRegistration,
                request.PackageVersionSpec,
                request.Notes);

            return RedirectToRoute(RouteName.Feed, new { name = feedName });
        }
    }
}