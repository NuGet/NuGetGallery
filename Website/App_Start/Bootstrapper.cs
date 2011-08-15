using System.Linq;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Routing;
using Elmah.Contrib.Mvc;
using Migrator.Framework;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGetGallery.Bootstrapper), "Start")]
namespace NuGetGallery {
    public static class Bootstrapper {
        public static void Start() {
            UpdateDatabase();
            Routes.RegisterRoutes(RouteTable.Routes);

            // TODO: move profile bootstrapping and container bootstrapping to here
            GlobalFilters.Filters.Add(new ElmahHandleErrorAttribute());
        }

        private static void UpdateDatabase() {
            var version = typeof(Bootstrapper).Assembly.GetTypes()
                .Where(type => typeof(Migration).IsAssignableFrom(type))
                .SelectMany(x => x.GetCustomAttributes(typeof(MigrationAttribute), false))
                .Max(x => ((MigrationAttribute)x).Version);

            var migrator = new Migrator.Migrator(
                "SqlServer",
                WebConfigurationManager.ConnectionStrings["NuGetGallery"].ConnectionString,
                typeof(Bootstrapper).Assembly);

            migrator.MigrateTo(version);
        }
    }
}