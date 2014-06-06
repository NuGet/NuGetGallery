using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Authorize]
    public partial class FeedsController : AppController
    {
        public IManageFeedService ManageFeedService { get; protected set; }
        public ISearchService SearchService { get; protected set; }

        protected FeedsController() { }

        public FeedsController(IManageFeedService manageFeedService,
            ISearchService searchService)
        {
            ManageFeedService = manageFeedService;
            SearchService = searchService;
        }

        [HttpGet]
        public virtual ActionResult Feed(string name)
        {
            var feed = ManageFeedService.GetFeedByName(name);
            if (feed == null)
            {
                return HttpNotFound();
            }

            if (feed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            List<string> managers = feed.Managers.Select(user => user.Username).ToList();
            List<FeedViewModel.FeedRuleDesc> rules = feed.Rules.Select(rule => new FeedViewModel.FeedRuleDesc(rule)).ToList();

            object model = new FeedViewModel
            {
                Name = feed.Name,
                Managers = managers,
                Inclusive = feed.Inclusive,
                FeedRules = rules
            };

            return View(model);
        }
    }
}
