using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Web;
using System.Web.DynamicData;
using System.Web.Routing;
using DynamicData.EFCodeFirstProvider;
using NuGetGallery;
using NuGetGallery.Data;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public class Registration
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "Although this type is mutable, we use it as immutable in our code.")]
        public static readonly MetaModel DefaultModel = new MetaModel() { DynamicDataFolderVirtualPath = "~/Areas/Admin/DynamicData" };

        public static void Register(RouteCollection routes, string root, IConfiguration configuration)
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
            routes.Insert(
                0,
                new DynamicDataRoute(root + "/{table}/{action}")
                {
                    Constraints = new RouteValueDictionary(new { action = "List|Details|Edit|Insert" }),
                    Model = DefaultModel
                });

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