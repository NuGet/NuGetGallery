using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
            // Start the runner.
            // Right now, we only run a single runner thread because I haven't quite worked out
            // how to capture the ETL events for a particular Invocation while it jumps between Task threads.
            _runner.Run(_cancelSource.Token).Wait();

            WorkerEventSource.Log.Stopped();
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
                var monitor = ConfigureMonitoring(config);
                var dispatcher = DiscoverJobs(config, monitor);

                _runner = new JobRunner(dispatcher, config, monitor);

                WorkerEventSource.Log.StartupComplete();
                return base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                WorkerEventSource.Log.StartupError(ex);
                return false;
            }
        }

        private JobDispatcher DiscoverJobs(BackendConfiguration config, BackendMonitoringHub monitor)
        {
            var jobs = typeof(WorkerRole)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(Job).IsAssignableFrom(t))
                .Select(t => Activator.CreateInstance(t))
                .Cast<Job>();
            return new JobDispatcher(config, jobs, monitor);
        }

        private BackendMonitoringHub ConfigureMonitoring(BackendConfiguration config)
        {
            var connectionString = config.Get("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString");
            var logDirectory = RoleEnvironment.GetLocalResource("Logs").RootPath;
            var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGetWorkerTemp");
            var monitoring = new BackendMonitoringHub(
                connectionString, 
                logDirectory,
                tempDirectory,
                RoleEnvironment.CurrentRoleInstance.Id);
            monitoring.Start();
            return monitoring;
        }
    }
}
