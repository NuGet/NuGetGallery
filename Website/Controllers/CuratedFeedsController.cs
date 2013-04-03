using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NuGet;
using NuGetGallery;

namespace NuGetGallery
{
    [Authorize]
    public partial class CuratedFeedsController : AppController
    {
        public const string ControllerName = "CuratedFeeds";

        public ICuratedFeedService CuratedFeedService { get; protected set; }
        public ISearchService SearchService { get; protected set; }

        protected CuratedFeedsController() { }

        public CuratedFeedsController(ICuratedFeedService curatedFeedService,
            ISearchService searchService)
        {
            CuratedFeedService = curatedFeedService;
            SearchService = searchService;
        }

        [HttpGet]
        public virtual ActionResult CuratedFeed(string name)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute(name, includePackages: true);
            if (curatedFeed == null)
            {
                return HttpNotFound();
            }

            if (curatedFeed.Managers.All(manager => manager.Username != Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            return View(
                new CuratedFeedViewModel
                    {
                        Name = curatedFeed.Name,
                        Managers = curatedFeed.Managers.Select(user => user.Username),
                        IncludedPackages = curatedFeed.Packages
                            .Where(cp => cp.Included)
                            .Select(
                                cp =>
                                new CuratedFeedViewModel.IncludedPackage
                                    { Id = cp.PackageRegistration.Id, AutomaticallyCurated = cp.AutomaticallyCurated }),
                        ExcludedPackages = curatedFeed.Packages
                            .Where(cp => !cp.Included)
                            .Select(cp => cp.PackageRegistration.Id),
                    });
        }

        [HttpGet]
        public virtual ActionResult ListPackages(string curatedFeedName, string q, string sortOrder = null, int page = 1, bool prerelease = false)
        {
            if (page < 1)
            {
                page = 1;
            }

            q = (q ?? "").Trim();

            if (String.IsNullOrEmpty(sortOrder))
            {
                // Determine the default sort order. If no query string is specified, then the sortOrder is DownloadCount
                // If we are searching for something, sort by relevance.
                sortOrder = q.IsEmpty() ? Constants.PopularitySortOrder : Constants.RelevanceSortOrder;
            }

            var searchFilter = SearchAdaptor.GetSearchFilter(q, sortOrder, page, prerelease);
            searchFilter.CuratedFeedKey = CuratedFeedService.GetKey(curatedFeedName);
            if (searchFilter.CuratedFeedKey == 0)
            {
                return HttpNotFound();
            }

            int totalHits;
            IQueryable<Package> packageVersions = SearchService.Search(searchFilter, out totalHits);
            if (page == 1 && !packageVersions.Any())
            {
                // In the event the index wasn't updated, we may get an incorrect count. 
                totalHits = 0;
            }

            var viewModel = new PackageListViewModel(
                packageVersions,
                q,
                sortOrder,
                totalHits,
                page - 1,
                Constants.DefaultPackageListPageSize,
                Url,
                prerelease);

            ViewBag.SearchTerm = q;

            return View("~/Views/Packages/ListPackages.cshtml", viewModel);
        }
    }
}