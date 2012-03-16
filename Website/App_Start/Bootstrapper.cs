using System;
using System.Data.Entity.Migrations;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Elmah;
using Elmah.Contrib.Mvc;
using NuGetGallery.Jobs;
using NuGetGallery.Migrations;
using WebBackgrounder;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGetGallery.Bootstrapper), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(NuGetGallery.Bootstrapper), "Stop")]
namespace NuGetGallery
{
    public static class Bootstrapper
    {
        private static readonly HttpApplication _elmahHttpApplication = new ElmahSignalScopeHttpApplication();
        private static JobManager _jobManager;

        private static JobManager CreateJobManager()
        {
            var jobs = new IJob[] { 
                new UpdateStatisticsJob(TimeSpan.FromSeconds(10), () => new EntitiesContext(), timeout: TimeSpan.FromMinutes(5)),
                new WorkItemCleanupJob(TimeSpan.FromDays(1), () => new EntitiesContext(), timeout: TimeSpan.FromDays(4)),
                new LuceneIndexingJob(TimeSpan.FromMinutes(10), timeout: TimeSpan.FromMinutes(2)),
            };

            var jobCoordinator = new WebFarmJobCoordinator(new EntityWorkItemRepository(() => new EntitiesContext()));
            var manager = new JobManager(jobs, jobCoordinator);
            manager.Fail(e => ErrorLog.GetDefault(null).Log(new Error(e)));
            return manager;
        }

        public static void Start()
        {
            UpdateDatabase();
            Routes.RegisterRoutes(RouteTable.Routes);

            DynamicDataEFCodeFirst.Registration.Register(RouteTable.Routes);

            // TODO: move profile bootstrapping and container bootstrapping to here
            GlobalFilters.Filters.Add(new ElmahHandleErrorAttribute());

            ValueProviderFactories.Factories.Add(new HttpHeaderValueProviderFactory());
            _jobManager = CreateJobManager();
            _jobManager.Start();
        }

        public static void Stop()
        {
            _jobManager.Dispose();
            _elmahHttpApplication.Dispose();
        }

        private static void UpdateDatabase()
        {
            var dbMigrator = new DbMigrator(new Settings());
            dbMigrator.Update();
            // The Seed method of Settings is never called, so 
            // we call it here again as a workaround.

            using (var context = new EntitiesContext())
            {
                Settings.SeedDatabase(context);
            }
        }

        /// <summary>
        /// Elmah requires an HttpApplication for its API. It uses it to determine when 
        /// to dispose of its signal instance. At this point, we don't have an HttpApplication 
        /// so I'll just create a stub one and control its lifecycle here.
        /// </summary>
        private class ElmahSignalScopeHttpApplication : HttpApplication
        {
        }
    }
}