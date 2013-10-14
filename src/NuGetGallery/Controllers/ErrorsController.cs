using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class ErrorsController : Controller
    {
        public virtual ActionResult NotFound()
        {
            return View();
        }

        public virtual ActionResult InternalError()
        {
            return View();
        }
    }
}