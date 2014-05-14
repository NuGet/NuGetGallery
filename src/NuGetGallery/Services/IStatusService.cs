using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IStatusService
    {
        Task<ActionResult> GetStatus();
    }
}