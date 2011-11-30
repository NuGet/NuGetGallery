using System;
using System.Web;
using Elmah;
using NuGetGallery.Jobs;
using WebBackgrounder;

[assembly: WebActivator.PostApplicationStartMethod(typeof(NuGetGallery.BackgroundTasksSetup), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(NuGetGallery.BackgroundTasksSetup), "Stop")]
namespace NuGetGallery
{
    public static class BackgroundTasksSetup
    {
        private static readonly HttpApplication _elmahHttpApplication = new ElmahSignalScopeHttpApplication();
        private static readonly JobManager _jobManager = CreateJobManager();

        private static JobManager CreateJobManager()
        {
            var jobs = new IJob[] { 
                new UpdateStatisticsJob(TimeSpan.FromSeconds(10), () => new EntitiesContext(), timeout: TimeSpan.FromMinutes(5)),
                new WorkItemCleanupJob(TimeSpan.FromDays(1), () => new EntitiesContext(), timeout: TimeSpan.FromDays(4)),
                new LuceneIndexingJob(TimeSpan.FromMinutes(10), timeout: TimeSpan.FromMinutes(2)),
            };

            var jobCoordinator = new WebFarmJobCoordinator(new EntityWorkItemRepository(() => new EntitiesContext()));
            var manager = new JobManager(jobs, jobCoordinator);
            manager.Fail(e => Elmah.ErrorLog.GetDefault(null).Log(new Error(e)));
            return manager;
        }

        public static void Start()
        {
            _jobManager.Start();
        }

        public static void Stop()
        {
            _jobManager.Dispose();
            _elmahHttpApplication.Dispose();
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