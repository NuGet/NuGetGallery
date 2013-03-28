using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Web.DynamicData;
using System.Web.Routing;
using DynamicData.EFCodeFirstProvider;
using NuGetGallery.Data;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class DynamicDataManager
    {
        public static MetaModel DefaultModel = new MetaModel() { DynamicDataFolderVirtualPath = "~/Areas/Admin/DynamicData" };

        private static DynamicDataRoute _route;
        
        public static void Register(RouteCollection routes, string root, IEntitiesContextFactory contextFactory)
        {
            try
            {
                DefaultModel.RegisterContext(
                    new EFCodeFirstDataModelProvider(
                        () => contextFactory.Create(readOnly: false)), // DB Admins do not need to respect read-only mode.
                        configuration: new ContextConfiguration { ScaffoldAllTables = true });
            }
            catch (SqlException e)
            {
                QuietlyLogException(e);
                return;
            }
            catch (DataException e)
            {
                QuietlyLogException(e);
                return;
            }

            // This route must come first to prevent some other route from the site to take over
            _route = new DynamicDataRoute(root + "/{table}/{action}")
            {
                Constraints = new RouteValueDictionary(new { action = "List|Details|Edit|Insert" }),
                Model = DefaultModel
            };
            routes.Insert(0, _route);

            routes.MapPageRoute(
                "dd_default",
                root,
                "~/Areas/Admin/DynamicData/Default.aspx");
        }

        private static void QuietlyLogException(Exception e)
        {
            try
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(e);
            }
            catch
            {
                // logging failed, don't allow exception to escape
            }
        }
    }
}