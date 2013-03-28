using System.Web.Mvc;
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
            var contextFactory = Container.Kernel.Get<IEntitiesContextFactory>();

            context.Routes.Ignore("Admin/Errors/{*pathInfo}"); // ELMAH owns this root
            DynamicDataManager.Register(context.Routes, "Admin/Database", contextFactory);

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
