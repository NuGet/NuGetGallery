using System;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Jobs.Monitoring;
using System.Net;
using NuGet.Services.Storage;
using NuGet.Services.Jobs.Configuration;

namespace NuGet.Services.Jobs
{
    public class JobsService : NuGetService
    {
        internal const string InvocationLogsContainerBaseName = "jobs-invocationlogs";
        public static readonly string MyServiceName = "Jobs";

        private AzureTable<JobDescription> _jobsTable;

        public IEnumerable<JobDescription> Jobs { get; private set; }
        
        public JobsService(ServiceHost host)
            : base(MyServiceName, host)
        {
        }

        protected override Task<bool> OnStart()
        {
            try
            {
                DiscoverJobs();

                return base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                JobsServiceEventSource.Log.StartupError(ex);
                return Task.FromResult(false);
            }
        }

        protected override Task OnRun()
        {
            var queueConfig = Configuration.GetSection<QueueConfiguration>();
            var dispatcher = new JobDispatcher(Jobs);
            var runner = new JobRunner(dispatcher, this, queueConfig.PollInterval);

            // Throttle heartbeats from the runner to every five minutes
            var interval = Configuration.Get<TimeSpan>("Service.HeartbeatInterval", TimeSpan.TryParse, TimeSpan.FromMinutes(5));
            Observable.FromEventPattern(runner, "Heartbeat")
                .Window(interval)
                .Subscribe(_ => Heartbeat());

            return runner.Run(Host.ShutdownToken);
        }

        /// <summary>
        /// Registers a job with the monitoring hub
        /// </summary>
        /// <param name="job">The job to register</param>
        public virtual void RegisterJob(JobDescription job)
        {
            // Record the discovery in the trace
            JobsServiceEventSource.Log.JobDiscovered(job);

            // Log an entry for the job in the status table
            _jobsTable.Merge(job);
        }

        private void DiscoverJobs()
        {
            _jobsTable = Storage.Primary.Tables.Table<JobDescription>();

            Jobs = typeof(JobsWorkerRole)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(JobBase).IsAssignableFrom(t))
                .Select(t => JobDescription.Create(t))
                .Where(d => d != null);

            foreach (var job in Jobs)
            {
                RegisterJob(job);
            }
        }
    }
}
