using System.Web.Mvc;

namespace NuGetGallery {
    public class PagesController : Controller {
        public const string Name = "Pages";

        public ActionResult Home() {
            return View();
        }
    }
}
