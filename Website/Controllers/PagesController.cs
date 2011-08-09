using System.Web.Mvc;

namespace NuGetGallery
{
    public class PagesController : Controller
    {
        public const string Name = "Pages";

        [ActionName(ActionName.Home)]
        public ActionResult ShowHomePage()
        {
            return View();
        }

        [ActionName(ActionName.Contribute)]
        public ActionResult ShowContributePage()
        {
            return View();
        }
    }
}
