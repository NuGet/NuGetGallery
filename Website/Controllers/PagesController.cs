using System.Web.Mvc;

namespace NuGetGallery {
    public partial class PagesController : Controller {
        public virtual ActionResult Home() {
            return View();
        }
    }
}
