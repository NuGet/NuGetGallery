using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace NuGetGallery.Operations.Worker
{
    public class WorkerRole : RoleEntryPoint
    {
        private JobRunner _runner;

        public WorkerRole() : this(null) { }

        public WorkerRole(Settings settings)
        {
            // Configure NLog
            LoggingConfiguration config = new LoggingConfiguration();
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget();
            config.AddTarget("console", consoleTarget);
            consoleTarget.Layout = "${date:format=HH\\:MM\\:ss} [${logger}] ${message}";

            LoggingRule rule = new LoggingRule("*", NLog.LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule);

            if (RoleEnvironment.IsAvailable)
            {
                ConfigureAzureLogging(config);
            }

            LogManager.Configuration = config;

            _runner = LoadJobRunner(settings);
        }

        public override bool OnStart()
        {
            return _runner.OnStart();
        }

        public override void OnStop()
        {
            _runner.OnStop();
        }

        public override void Run()
        {
            _runner.Run();
        }

        public void RunSingleJob(string jobName)
        {
            _runner.RunSingleJob(jobName);
        }

        public void Stop()
        {
            _runner.Stop();
        }

        public static IEnumerable<string> GetJobList()
        {
            JobRunner runner = LoadJobRunner(new Settings());
            return runner.Jobs.Keys;
        }

        public static void Execute(string jobName, bool continuous, IDictionary<string, string> overrideSettings)
        {
            // Create the settings manager
            var settings = new Settings(overrideSettings);

            // Get a job runner
            JobRunner runner = LoadJobRunner(settings);

            // See which mode we're in
            if (String.IsNullOrWhiteSpace(jobName))
            {
                // Run ALL THE JOBS!
                runner.OnStart();
                runner.Run();
                Console.WriteLine("Worker is running. Press ENTER to stop");
                Console.ReadLine();
                runner.Stop();
                runner.OnStop();
            }
            else
            {
                // Run JUST ONE JOB!
                if (!continuous)
                {
                    runner.RunSingleJob(jobName);
                }
                else
                {
                    runner.RunSingleJobContinuously(jobName);
                }
            }
        }

        private static void ConfigureAzureLogging(LoggingConfiguration config)
        {
            // Configure NLog to write to trace
            var traceTarget = new CustomTraceTarget();
            traceTarget.Layout = "${longdate} [${logger}] [${level:lowercase=true}] ${message}";
            config.AddTarget("trace", traceTarget);

            // Send everything to trace, it will sort it out.
            var traceRule = new LoggingRule("*", NLog.LogLevel.Debug, traceTarget);
            config.LoggingRules.Add(traceRule);

            // Configure trace listener
            Trace.Listeners.Add(new DiagnosticMonitorTraceListener());

            // Configure Diagnostics
            var diagconfig = DiagnosticMonitor.GetDefaultInitialConfiguration();
            diagconfig.OverallQuotaInMB = 2048;
            diagconfig.DiagnosticInfrastructureLogs.BufferQuotaInMB = 512;
            diagconfig.Directories.DataSources.Clear();
            diagconfig.Logs.BufferQuotaInMB = 1024;
            diagconfig.Logs.ScheduledTransferLogLevelFilter = Microsoft.WindowsAzure.Diagnostics.LogLevel.Undefined;
            diagconfig.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
            DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", diagconfig);
        }

        private static JobRunner LoadJobRunner(Settings settings)
        {
            AssemblyCatalog catalog = new AssemblyCatalog(typeof(WorkerRole).Assembly);
            var container = new CompositionContainer(catalog);

            // Load settings
            settings = settings ?? new Settings();
            container.ComposeExportedValue(settings);

            // Get the job runner
            return container.GetExportedValue<JobRunner>();
        }
    }
}
