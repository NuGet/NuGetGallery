using System;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Elmah;
using Elmah.Contrib.Mvc;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using NuGetGallery.Jobs;
using NuGetGallery.Migrations;
using StackExchange.Profiling;
using StackExchange.Profiling.MVCHelpers;
using WebBackgrounder;

[assembly: WebActivator.PreApplicationStartMethod(typeof(NuGetGallery.AppActivator), "PreStart")]
[assembly: WebActivator.PostApplicationStartMethod(typeof(NuGetGallery.AppActivator), "PostStart")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(NuGetGallery.AppActivator), "Stop")]

namespace NuGetGallery
{
    public static class AppActivator
    {
        private static JobManager _jobManager;
        private static readonly Bootstrapper _ninjectBootstrapper = new Bootstrapper();

        public static void PreStart()
        {
            NinjectPreStart();
            MiniProfilerPreStart();
        }

        public static void PostStart()
        {
            MiniProfilerPostStart();
            DbMigratorPostStart();
            BackgroundJobsPostStart();
            AppPostStart();
            DynamicDataPostStart();
        }

        public static void Stop()
        {
            BackgroundJobsStop();
            NinjectStop();
        }

        private static void AppPostStart()
        {
            Routes.RegisterRoutes(RouteTable.Routes);
            GlobalFilters.Filters.Add(new ElmahHandleErrorAttribute());
            ValueProviderFactories.Factories.Add(new HttpHeaderValueProviderFactory());
        }

        private static void BackgroundJobsPostStart()
        {
            var jobs = new IJob[] { 
                new UpdateStatisticsJob(TimeSpan.FromSeconds(10), () => new EntitiesContext(), timeout: TimeSpan.FromMinutes(5)),
                new WorkItemCleanupJob(TimeSpan.FromDays(1), () => new EntitiesContext(), timeout: TimeSpan.FromDays(4)),
                new LuceneIndexingJob(TimeSpan.FromMinutes(10), timeout: TimeSpan.FromMinutes(2)),
            };
            var jobCoordinator = new WebFarmJobCoordinator(new EntityWorkItemRepository(() => new EntitiesContext()));
            _jobManager = new JobManager(jobs, jobCoordinator);
            _jobManager.Fail(e => ErrorLog.GetDefault(null).Log(new Error(e)));
            _jobManager.Start();
        }

        private static void BackgroundJobsStop()
        {
            _jobManager.Dispose();
        }
        
        private static void DbMigratorPostStart()
        {
            var dbMigrator = new DbMigrator(new MigrationsConfiguration());
            // After upgrading to EF 4.3 and MiniProfile 1.9, there is a bug that causes several 
            // 'Invalid object name 'dbo.__MigrationHistory' to be thrown when the database is first created; 
            // it seems these can safely be ignored, and the database will still be created.
            dbMigrator.Update();
        }

        private static void DynamicDataPostStart()
        {
            DynamicDataEFCodeFirst.Registration.Register(RouteTable.Routes);
        }

        private static void MiniProfilerPreStart()
        {
            MiniProfilerEF.Initialize();
            DynamicModuleUtility.RegisterModule(typeof(MiniProfilerStartupModule));
            GlobalFilters.Filters.Add(new ProfilingActionFilter());
        }

        private static void MiniProfilerPostStart()
        {
            var copy = ViewEngines.Engines.ToList();
            ViewEngines.Engines.Clear();
            foreach (var item in copy)
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
        }

        private static void NinjectPreStart()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            DynamicModuleUtility.RegisterModule(typeof(HttpApplicationInitializationModule));
            _ninjectBootstrapper.Initialize(() => Container.Kernel);
        }

        private static void NinjectStop()
        {
            _ninjectBootstrapper.ShutDown();
        }

        private class MiniProfilerStartupModule : IHttpModule
        {
            public void Init(HttpApplication context)
            {
                context.BeginRequest += (sender, e) => MiniProfiler.Start();

                context.AuthorizeRequest += (sender, e) =>
                {
                    bool stopProfiling;
                    var httpContext = HttpContext.Current;

                    if (httpContext == null)
                        stopProfiling = true;
                    else
                    {
                        // Temporarily removing until we figure out the hammering of request we saw.
                        //var userCanProfile = httpContext.User != null && HttpContext.Current.User.IsInRole(Const.AdminRoleName);
                        var requestIsLocal = httpContext.Request.IsLocal;

                        //stopProfiling = !userCanProfile && !requestIsLocal
                        stopProfiling = !requestIsLocal;
                    }

                    if (stopProfiling)
                        MiniProfiler.Stop(true);
                };

                context.EndRequest += (sender, e) => MiniProfiler.Stop();
            }

            public void Dispose()
            {
            }
        }
    }
}