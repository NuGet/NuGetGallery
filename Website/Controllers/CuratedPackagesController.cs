using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Authorize]
    public partial class CuratedPackagesController : AppController
    {
        public const string ControllerName = "CuratedPackages";
        protected ICuratedFeedService CuratedFeedService { get; set; }

        protected CuratedPackagesController() { }

        public CuratedPackagesController(ICuratedFeedService curatedFeedService)
        {
            this.CuratedFeedService = curatedFeedService;
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

            if (curatedFeed.Managers.All(manager => manager.Username != Identity.Name))
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

            if (curatedFeed.Managers.All(manager => manager.Username != Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            GetService<IDeleteCuratedPackageCommand>().Execute(
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

            if (curatedFeed.Managers.All(manager => manager.Username != Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            if (!ModelState.IsValid)
            {
                return new HttpStatusCodeResult(400);
            }

            GetService<IModifyCuratedPackageCommand>().Execute(
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

            if (curatedFeed.Managers.All(manager => manager.Username != Identity.Name))
            {
                return new HttpStatusCodeResult(403);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            var packageRegistration = GetService<IPackageRegistrationByIdQuery>().Execute(request.PackageId, includePackages: false);
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

            GetService<ICreateCuratedPackageCommand>().Execute(
                curatedFeed,
                packageRegistration,
                notes: request.Notes);

            return RedirectToRoute(RouteName.CuratedFeed, new { name = curatedFeed.Name });
        }
    }
}