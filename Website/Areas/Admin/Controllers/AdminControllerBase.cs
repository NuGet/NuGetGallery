using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Areas.Admin.Controllers
{
    [Authorize(Roles="Admins")]
    public class AdminControllerBase : Controller
    {
    }
}