using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;

namespace NuGetGallery
{
    [Authorize]
    public partial class CuratedPackagesController : AppController
    {
        internal ICuratedFeedService CuratedFeedService { get; set; }
        internal IEntitiesContext EntitiesContext { get; set; }

        protected CuratedPackagesController() { }

        public CuratedPackagesController(
            ICuratedFeedService curatedFeedService,
            IEntitiesContext entitiesContext)
        {
            this.CuratedFeedService = curatedFeedService;
            this.EntitiesContext = entitiesContext;
        }

        [ActionName("CreateCuratedPackageForm")]
        [HttpGet]
        public virtual ActionResult GetCreateCuratedPackageForm(string curatedFeedName)
        {
            var curatedFeed = CuratedFeedService.GetFeedByName(curatedFeedName, includePackages: false);
            if (curatedFeed == null)
            {
                return HttpNotFound();
            }

            if (curatedFeed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            ViewBag.CuratedFeedName = curatedFeed.Name;
            return View();
        }

        [ActionName("CuratedPackage")]
        [HttpDelete]
        public virtual ActionResult DeleteCuratedPackage(
            string curatedFeedName,
            string curatedPackageId)
        {
            var curatedFeed = CuratedFeedService.GetFeedByName(curatedFeedName, includePackages: true);
            if (curatedFeed == null)
            {
                return HttpNotFound();
            }

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.PackageRegistration.Id == curatedPackageId);
            if (curatedPackage == null)
            {
                return HttpNotFound();
            }

            if (curatedFeed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            CuratedFeedService.DeleteCuratedPackage(
                curatedFeed.Key,
                curatedPackage.Key);

            return new HttpStatusCodeResult(204);
        }

        [ActionName("CuratedPackage")]
        [AcceptVerbs("patch")]
        public virtual ActionResult PatchCuratedPackage(
            string curatedFeedName,
            string curatedPackageId,
            ModifyCuratedPackageRequest request)
        {
            var curatedFeed = CuratedFeedService.GetFeedByName(curatedFeedName, includePackages: true);
            if (curatedFeed == null)
            {
                return HttpNotFound();
            }

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.PackageRegistration.Id == curatedPackageId);
            if (curatedPackage == null)
            {
                return HttpNotFound();
            }

            if (curatedFeed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            if (!ModelState.IsValid)
            {
                return new HttpStatusCodeResult(400);
            }

            CuratedFeedService.ModifyCuratedPackage(
                curatedFeed.Key,
                curatedPackage.Key,
                request.Included);

            return new HttpStatusCodeResult(204);
        }

        [ActionName("CuratedPackages")]
        [HttpPost]
        public virtual ActionResult PostCuratedPackages(
            string curatedFeedName,
            CreateCuratedPackageRequest request)
        {
            var curatedFeed = CuratedFeedService.GetFeedByName(curatedFeedName, includePackages: true);
            if (curatedFeed == null)
            {
                return HttpNotFound();
            }

            if (curatedFeed.Managers.All(manager => manager.Username != User.Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            var packageRegistration = EntitiesContext.PackageRegistrations
                .Where(pr => pr.Id == request.PackageId)
                .Include(pr => pr.Owners).FirstOrDefault();

            if (packageRegistration == null)
            {
                ModelState.AddModelError("PackageId", Strings.PackageWithIdDoesNotExist);
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            if (curatedFeed.Packages.Any(cp => cp.PackageRegistration.Key == packageRegistration.Key))
            {
                ModelState.AddModelError("PackageId", Strings.PackageIsAlreadyCurated);
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            CuratedFeedService.CreatedCuratedPackage(
                curatedFeed,
                packageRegistration,
                included: true,
                automaticallyCurated: false,
                notes: request.Notes);

            return RedirectToRoute(RouteName.CuratedFeed, new { name = curatedFeed.Name });
        }
    }
}