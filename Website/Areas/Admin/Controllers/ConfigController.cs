using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class ConfigController : AdminControllerBase
    {
        private readonly IConfiguration _config;

        public ConfigController(IConfiguration config)
        {
            _config = config;
        }

        public virtual ActionResult Index()
        {
            var dict = (from p in typeof(IConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        where p.CanRead
                        select p)
                       .ToDictionary(p => p.Name, p => p.GetValue(_config));

            return View(dict);
        }
    }
}