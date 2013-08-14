using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Web.DynamicData;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.UI;
using DynamicData.EFCodeFirstProvider;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class DynamicDataManager
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "We do treat this as immutable.")]
        public static readonly MetaModel DefaultModel = new MetaModel() { DynamicDataFolderVirtualPath = "~/Areas/Admin/DynamicData" };

        private static DynamicDataRoute _route;
        
        public static void Register(RouteCollection routes, string root, IAppConfiguration configuration)
        {
            // Set up unobtrusive validation
            InitializeValidation();

            // Set up dynamic data
            InitializeDynamicData(routes, root, configuration);
        }

        private static void InitializeValidation()
        {
        }

        private static void InitializeDynamicData(RouteCollection routes, string root, IAppConfiguration configuration)
        {
            try
            {
                DefaultModel.RegisterContext(
                    new EFCodeFirstDataModelProvider(
                        () => new EntitiesContext(configuration.SqlConnectionString, readOnly: false)), // DB Admins do not need to respect read-only mode.
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
