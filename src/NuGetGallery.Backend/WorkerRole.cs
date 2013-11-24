using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
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
        private BackendConfiguration _config;
        private IEnumerable<JobDescription> _jobs;
        private CancellationTokenSource _cancelSource = new CancellationTokenSource();

        public override void Run()
        {
            // Run as many job executors as there are processors
            var runnerTasks = Enumerable
                .Range(0, Environment.ProcessorCount)
                .Select(index => ExecuteJobs(index, _cancelSource.Token))
                .ToArray();
            
            Task.WaitAll(runnerTasks);

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

                _config = BackendConfiguration.CreateAzure();
                DiscoverJobs();

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

        public async Task ExecuteJobs(int index, CancellationToken cancelToken)
        {
            var instanceName = RoleEnvironment.CurrentRoleInstance.Id + "_T" + index.ToString();
            var threadName = "Thread" + index.ToString();
            RunnerId.Set(index);

            var monitor = ConfigureMonitoring(instanceName, threadName, _config);
            var dispatcher = new JobDispatcher(_config, _jobs, monitor);
            var runner = new JobRunner(dispatcher, _config, monitor);
            
            await runner.Run(cancelToken);
        }

        private void DiscoverJobs()
        {
            _jobs = typeof(WorkerRole)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(JobBase).IsAssignableFrom(t))
                .Select(t => JobDescription.Create(t))
                .Where(d => d != null);

            foreach (var job in _jobs)
            {
                WorkerEventSource.Log.JobDiscovered(job);
            }
        }

        private BackendMonitoringHub ConfigureMonitoring(string instanceName, string threadName, BackendConfiguration config)
        {
            var connectionString = config.Get("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString");
            var logDirectory = Path.Combine(RoleEnvironment.GetLocalResource("Logs").RootPath, threadName);
            var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGetWorkerTemp", threadName);
            var monitoring = new BackendMonitoringHub(
                connectionString, 
                logDirectory,
                tempDirectory,
                instanceName);
            monitoring.Start();
            return monitoring;
        }
    }
}
