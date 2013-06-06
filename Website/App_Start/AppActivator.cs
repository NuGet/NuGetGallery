using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Elmah;
using Elmah.Contrib.Mvc;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Jobs;
using NuGetGallery.Jobs;
using NuGetGallery.Migrations;
using WebActivator;
using WebBackgrounder;

[assembly: WebActivator.PreApplicationStartMethod(typeof(AppActivator), "PreStart")]
[assembly: PostApplicationStartMethod(typeof(AppActivator), "PostStart")]
[assembly: ApplicationShutdownMethod(typeof(AppActivator), "Stop")]

namespace NuGetGallery
{
    public static class AppActivator
    {
        private static JobManager _jobManager;
        private static readonly Bootstrapper NinjectBootstrapper = new Bootstrapper();

        public static void PreStart()
        {
            NinjectPreStart();
            ElmahPreStart();
            GlimpsePreStart();
        }

        public static void PostStart()
        {
            // Get configuration from the kernel
            var config = Container.Kernel.Get<IAppConfiguration>();
            DbMigratorPostStart();
            BackgroundJobsPostStart(config);
            AppPostStart();
            BundlingPostStart();
        }

        public static void Stop()
        {
            BackgroundJobsStop();
            NinjectStop();
        }

        private static void GlimpsePreStart()
        {
            DynamicModuleUtility.RegisterModule(typeof(Glimpse.AspNet.HttpModule));
        }

        private static void BundlingPostStart()
        {
            var scriptBundle = new ScriptBundle("~/bundles/js")
                .Include("~/Scripts/jquery-{version}.js")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js")
                .Include("~/Scripts/nugetgallery.js");
            BundleTable.Bundles.Add(scriptBundle);

            // Modernizr needs to be delivered at the top of the page but putting it in a bundle gets us a cache-buster.
            // TODO: Use minified modernizr!
            var modernizrBundle = new ScriptBundle("~/bundles/modernizr")
                .Include("~/Scripts/modernizr-2.0.6-development-only.js");
            BundleTable.Bundles.Add(modernizrBundle);

            var stylesBundle = new StyleBundle("~/bundles/css")
                .Include("~/Content/site.css");
            BundleTable.Bundles.Add(stylesBundle);

        }

        private static void ElmahPreStart()
        {
            ServiceCenter.Current = _ => Container.Kernel;
        }

        private static void AppPostStart()
        {
            Routes.RegisterRoutes(RouteTable.Routes);
            Routes.RegisterServiceRoutes(RouteTable.Routes);
            AreaRegistration.RegisterAllAreas();
            
            GlobalFilters.Filters.Add(new ElmahHandleErrorAttribute());
            GlobalFilters.Filters.Add(new ReadOnlyModeErrorFilter());
            GlobalFilters.Filters.Add(new RequireRemoteHttpsAttribute() { OnlyWhenAuthenticated = true });
            ValueProviderFactories.Factories.Add(new HttpHeaderValueProviderFactory());
        }

        private static void BackgroundJobsPostStart(IAppConfiguration configuration)
        {
            var jobs = configuration.HasWorker ?
                new IJob[]
                {
                    new LuceneIndexingJob(TimeSpan.FromMinutes(10), () => new EntitiesContext(configuration.SqlConnectionString, readOnly: true), timeout: TimeSpan.FromMinutes(2))
                }                
                    :
                new IJob[]
                {
                    // readonly: false workaround - let statistics background job write to DB in read-only mode since we don't care too much about losing that data
                    new UpdateStatisticsJob(TimeSpan.FromMinutes(5), 
                        () => new EntitiesContext(configuration.SqlConnectionString, readOnly: false), 
                        timeout: TimeSpan.FromMinutes(5)),
                    new LuceneIndexingJob(TimeSpan.FromMinutes(10), () => new EntitiesContext(configuration.SqlConnectionString, readOnly: true), timeout: TimeSpan.FromMinutes(2))
                };
            var jobCoordinator = new NuGetJobCoordinator();
            _jobManager = new JobManager(jobs, jobCoordinator)
                {
                    RestartSchedulerOnFailure = true
                };
            _jobManager.Fail(e => ErrorLog.GetDefault(null).Log(new Error(e)));
            _jobManager.Start();
        }

        private static void BackgroundJobsStop()
        {
            _jobManager.Dispose();
        }

        private static void DbMigratorPostStart()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<EntitiesContext>(null);
        }

        private static void NinjectPreStart()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            DynamicModuleUtility.RegisterModule(typeof(HttpApplicationInitializationModule));
            NinjectBootstrapper.Initialize(() => Container.Kernel);
        }

        private static void NinjectStop()
        {
            NinjectBootstrapper.ShutDown();
        }
    }
}
