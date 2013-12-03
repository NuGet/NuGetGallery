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
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
{
    public class WorkerRole : RoleEntryPoint
    {
        private ServiceConfiguration _config;
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

            JobsServiceEventSource.Log.Stopped();
        }

        public override void OnStop()
        {
            JobsServiceEventSource.Log.Stopping();

            _cancelSource.Cancel();
            base.OnStop();
        }

        public override bool OnStart()
        {
            JobsServiceEventSource.Log.Starting();

            try
            {
                // Set the maximum number of concurrent connections 
                ServicePointManager.DefaultConnectionLimit = 12;

                _config = ServiceConfiguration.CreateAzure();
                DiscoverJobs();

                JobsServiceEventSource.Log.StartupComplete();
                return base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                JobsServiceEventSource.Log.StartupError(ex);
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
                JobsServiceEventSource.Log.JobDiscovered(job);
            }
        }

        private BackendMonitoringHub ConfigureMonitoring(string instanceName, string threadName, ServiceConfiguration config)
        {
            var logDirectory = Path.Combine(RoleEnvironment.GetLocalResource("Logs").RootPath, threadName);
            var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGetWorkerTemp", threadName);
            var monitoring = new BackendMonitoringHub(
                config.Storage.Primary, 
                logDirectory,
                tempDirectory,
                instanceName);
            monitoring.Start();
            return monitoring;
        }
    }
}
