using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery
{
    [Authorize]
    public class CuratedPackagesController : AppController
    {
        public const string Name = "CuratedPackages";

        [HttpGet]
        public ActionResult CreateCuratedPackageForm(string curatedFeedName)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute(curatedFeedName);
            if (curatedFeed == null)
                return HttpNotFound();
            
            if (!curatedFeed.Managers.Any(manager => manager.Username == Identity.Name))
                return new HttpStatusCodeResult(403);

            ViewBag.CuratedFeedName = curatedFeed.Name;
            return View();
        }

        [HttpPost]
        public ActionResult CuratedPackages(
            string curatedFeedName,
            CreatedCuratedPackageRequest request)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute(curatedFeedName, includePackages: true);
            if (curatedFeed == null)
                return HttpNotFound();

            if (!curatedFeed.Managers.Any(manager => manager.Username == Identity.Name))
                return new HttpStatusCodeResult(403);

            if (!ModelState.IsValid)
            {
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            var packageRegistration = GetService<IPackageRegistrationByIdQuery>().Execute(request.PackageId);
            if (packageRegistration == null)
            {
                ModelState.AddModelError("PackageId", Strings.PackageWithIdDoesNotExist);
                ViewBag.CuratedFeedName = curatedFeed.Name;
                return View("CreateCuratedPackageForm");
            }

            GetService<ICreatedCuratedPackageCommand>().Execute(
                curatedFeed.Key,
                packageRegistration.Key,
                notes: request.Notes);

            return RedirectToRoute(RouteName.CuratedFeed, new { name = curatedFeed.Name });
        }

        [AcceptVerbs("patch")]
        public ActionResult CuratedPackage(
            string curatedFeedName,
            string curatedPackageId,
            ModifyCuratedPackageRequest request)
        {
            var curatedFeed = GetService<ICuratedFeedByNameQuery>().Execute(curatedFeedName, includePackages: true);
            if (curatedFeed == null)
                return HttpNotFound();

            var curatedPackage = curatedFeed.Packages.SingleOrDefault(cp => cp.PackageRegistration.Id == curatedPackageId);
            if (curatedPackage == null)
                return HttpNotFound();

            if (!curatedFeed.Managers.Any(manager => manager.Username == Identity.Name))
                return new HttpStatusCodeResult(403);

            if (!ModelState.IsValid)
                return new HttpStatusCodeResult(400);

            GetService<IModifyCuratedPackageCommand>().Execute(
                curatedFeed.Key,
                curatedPackage.Key,
                request.Included);

            return new HttpStatusCodeResult(200);
        }
    }
}
