using System.Web.DynamicData;
using System.Web.Routing;
using DynamicData.EFCodeFirstProvider;
using NuGetGallery;

namespace DynamicDataEFCodeFirst
{
    public class Registration
    {
        private static MetaModel s_defaultModel = new MetaModel();
        public static MetaModel DefaultModel
        {
            get
            {
                return s_defaultModel;
            }
        }

        public static void Register(RouteCollection routes)
        {
            DefaultModel.RegisterContext(
               new EFCodeFirstDataModelProvider(() => new EntitiesContext()),
               new ContextConfiguration() { ScaffoldAllTables = true });

            // This route must come first to prevent some other route from the site to take over
            routes.Insert(0, new DynamicDataRoute("dbadmin/{table}/{action}")
            {
                Constraints = new RouteValueDictionary(new { action = "List|Details|Edit|Insert" }),
                Model = DefaultModel
            });

            routes.MapPageRoute(
                "dd_default",
                "dbadmin",
                "~/DynamicData/Default.aspx");
        }
    }
}