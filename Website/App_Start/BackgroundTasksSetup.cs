using System;
using NuGetGallery.Jobs;
using WebBackgrounder;

[assembly: WebActivator.PostApplicationStartMethod(typeof(NuGetGallery.BackgroundTasksSetup), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(NuGetGallery.BackgroundTasksSetup), "Stop")]
namespace NuGetGallery
{
    public static class BackgroundTasksSetup
    {
        static JobManager _jobManager = CreateJobManager();

        private static JobManager CreateJobManager()
        {
            var jobs = new IJob[] { 
                new UpdateStatisticsJob(TimeSpan.FromSeconds(10), new EntitiesContext())
            };

            var jobCoordinator = new WebFarmJobCoordinator(new EntityWorkItemRepository(() => new EntitiesContext()));
            return new JobManager(jobs, jobCoordinator);
        }

        public static void Start()
        {
            _jobManager.Start();
        }

        public static void Stop()
        {
            _jobManager.Dispose();
        }
    }
}