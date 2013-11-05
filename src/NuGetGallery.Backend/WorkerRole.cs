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
using NuGetGallery.Backend.Tracing;

namespace NuGetGallery.Backend
{
    public class WorkerRole : RoleEntryPoint
    {
        private JobRunner _runner;
        private CancellationTokenSource _cancelSource;

        public override void Run()
        {
            _runner.Run(_cancelSource.Token);
        }

        public override void OnStop()
        {
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

                var config = BackendConfiguration.Load();
                var diagnostics = ConfigureDiagnostics(RoleEnvironment.GetConfigurationSettingValue("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"));
                var dispatcher = DiscoverJobs(config, diagnostics);

                var queue = config.PrimaryStorage.CreateCloudQueueClient().GetQueueReference("NuGetWorkerQueue");
                queue.CreateIfNotExists();

                _runner = new JobRunner(dispatcher, queue, diagnostics);

                return base.OnStart();
            }
            catch (Exception ex)
            {
                // Exceptions that escape to this level are fatal
                WorkerEventSource.Log.StartupFatal(ex.ToString(), ex.StackTrace);
                return false;
            }
        }

        private JobDispatcher DiscoverJobs(BackendConfiguration config, DiagnosticsManager diagnostics)
        {
            var jobs = typeof(WorkerRole)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(Job).IsAssignableFrom(t))
                .Select(t => Activator.CreateInstance(t))
                .Cast<Job>();
            var dispatcher = new JobDispatcher(config, jobs);
            foreach (var job in dispatcher.Jobs)
            {
                diagnostics.RegisterJob(job);
            }
            return dispatcher;
        }

        private DiagnosticsManager ConfigureDiagnostics(string diagnosticsStorage)
        {
            var logDirectory = RoleEnvironment.GetLocalResource("Logs").RootPath;
            var diagnostics = new DiagnosticsManager(logDirectory, diagnosticsStorage);
            diagnostics.Initialize();
            return diagnostics;
        }
    }
}
