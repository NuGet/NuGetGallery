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
using NuGet.Services.Work.Monitoring;
using System.Net;
using NuGet.Services.Storage;
using NuGet.Services.Work.Configuration;
using Autofac.Core;
using Autofac;
using NuGet.Services.ServiceModel;
using NuGet.Services.Work.Api.Models;
using NuGet.Services.Configuration;

namespace NuGet.Services.Work
{
    public class WorkService : NuGetService
    {
        internal const string InvocationLogsContainerBaseName = "work";
        public static readonly string MyServiceName = "Work";

        private AzureTable<JobDescription> _jobsTable;
        private JobRunner _runner;

        public IEnumerable<JobDescription> Jobs { get; private set; }
        
        public WorkService(ServiceHost host)
            : base(MyServiceName, host)
        {
        }

        protected override async Task<bool> OnStart()
        {
            try
            {
                await DiscoverJobs();

                return await base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                WorkServiceEventSource.Log.StartupError(ex);
                return false;
            }
        }

        protected override Task OnRun()
        {
            var queueConfig = Configuration.GetSection<QueueConfiguration>();
            _runner = Container.Resolve<JobRunner>();
            _runner.Heartbeat += (_, __) => Heartbeat();

            return _runner.Run(Host.ShutdownToken);
        }

        private async Task DiscoverJobs()
        {
            _jobsTable = Storage.Primary.Tables.Table<JobDescription>();

            Jobs = Container.Resolve<IEnumerable<JobDescription>>();

            await Task.WhenAll(Jobs.Select(j =>
            {
                // Record the discovery in the trace
                WorkServiceEventSource.Log.JobDiscovered(j);

                // Log an entry for the job in the status table
                return _jobsTable.Merge(j);
            }));
        }

        public override void RegisterComponents(ContainerBuilder builder)
        {
            base.RegisterComponents(builder);

            var jobdefs = typeof(WorkWorkerRole)
                   .Assembly
                   .GetExportedTypes()
                   .Where(t => !t.IsAbstract && typeof(JobBase).IsAssignableFrom(t))
                   .Select(t => JobDescription.Create(t))
                   .Where(d => d != null);
            builder.RegisterInstance(jobdefs).As<IEnumerable<JobDescription>>();

            builder.RegisterModule(new JobComponentsModule());
        }

        public override Task<object> GetCurrentStatus()
        {
            if (_runner != null)
            {
                return _runner.GetCurrentStatus();
            }
            return Task.FromResult<object>(null);
        }

        public override Task<object> Describe()
        {
            return Task.FromResult<object>(new WorkServiceModel(Jobs));
        }
    }
}
