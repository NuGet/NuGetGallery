using System.Web.Mvc;

namespace NuGetGallery.Controllers
{
    public partial class ErrorsController : Controller
    {
        // GET: /Errors/{name}
        public virtual ActionResult Page(string name)
        {
            return View(name);
        }
    }
}
