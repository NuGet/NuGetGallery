using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery.Areas.Admin.DynamicData;
using Ninject;
using NuGetGallery.Data;

namespace NuGetGallery.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "Admin";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.Routes.Ignore("Admin/Errors/{*catchAll}"); // ELMAH owns this root
            DynamicDataManager.Register(context.Routes,
                                  "Admin/Database",
                                  Container.Kernel.Get<IConfiguration>(),
                                  Container.Kernel.Get<IEntitiesContextFactory>());

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
