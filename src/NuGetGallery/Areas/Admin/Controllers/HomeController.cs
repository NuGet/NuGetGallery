using System;
using System.Diagnostics.CodeAnalysis;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class HomeController : AdminControllerBase
    {
        public virtual ActionResult Index()
        {
            return View();
        }

        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Throw", Justification="This is an admin action")]
        [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "This is an admin action")]
        public virtual ActionResult Throw()
        {
            throw new Exception("KA BOOM!");
        }
    }
}