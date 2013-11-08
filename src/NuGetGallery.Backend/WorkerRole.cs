using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Backend.Monitoring;

namespace NuGetGallery.Backend
{
    public class WorkerRole : RoleEntryPoint
    {
        private JobRunner _runner;
        private CancellationTokenSource _cancelSource = new CancellationTokenSource();

        public override void Run()
        {
            _runner.Run(_cancelSource.Token).Wait();
        }

        public override void OnStop()
        {
            WorkerEventSource.Log.Stopping();

            _cancelSource.Cancel();
            base.OnStop();
        }

        public override bool OnStart()
        {
            WorkerEventSource.Log.Starting();

            try
            {
                // Set the maximum number of concurrent connections 
                ServicePointManager.DefaultConnectionLimit = 12;

                var config = BackendConfiguration.CreateAzure();
                var monitoring = ConfigureMonitoring(config);
                var dispatcher = DiscoverJobs(config, monitoring);

                _runner = new JobRunner(dispatcher, config, monitoring);

                WorkerEventSource.Log.StartupComplete();
                return base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                WorkerEventSource.Log.StartupFatal(ex);
                return false;
            }
        }

        private JobDispatcher DiscoverJobs(BackendConfiguration config, BackendMonitoringHub monitoring)
        {
            var jobs = typeof(WorkerRole)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(Job).IsAssignableFrom(t))
                .Select(t => Activator.CreateInstance(t))
                .Cast<Job>();
            return new JobDispatcher(config, jobs);
        }

        private BackendMonitoringHub ConfigureMonitoring(BackendConfiguration config)
        {
            var connectionString = config.Get("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString");
            var logDirectory = RoleEnvironment.GetLocalResource("Logs").RootPath;
            var monitoring = new BackendMonitoringHub(connectionString, logDirectory);
            monitoring.Start();
            return monitoring;
        }
    }
}
