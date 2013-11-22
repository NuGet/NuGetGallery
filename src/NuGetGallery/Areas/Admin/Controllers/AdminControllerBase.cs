using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    [Authorize(Roles="Admins")]
    public class AdminControllerBase : AppController
    {
    }
}