// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Backend
{
    public class WorkerRole : RoleEntryPoint
    {
        private JobRunner _runner;
        private Logger _logger;

        public WorkerRole()
            : this(null)
        {
        }

        public WorkerRole(Settings settings)
        {
            string logDir = Path.Combine(Environment.CurrentDirectory, "Logs");

            try
            {
                // Configure NLog
                LoggingConfiguration config = new LoggingConfiguration();

                // Console Target
                bool inCloud = false;
                try
                {
                    inCloud = RoleEnvironment.IsAvailable;
                }
                catch { }

                // Get the logs resource if it exists and use it as the log dir
                try
                {
                    if (RoleEnvironment.IsAvailable)
                    {
                        LocalResource logsResource = RoleEnvironment.GetLocalResource("Logs");
                        logDir = logsResource.RootPath;
                    }
                }
                catch (Exception)
                {
                    // Just use basedir.
                }

                // File Target
                FileTarget jobLogTarget = new FileTarget()
                {
                    FileName = Path.Combine(logDir, "Jobs", "${logger:shortName=true}.${date:yyyy-MM-dd-HHmm}.log.json"),
                };
                ConfigureFileTarget(jobLogTarget);
                config.AddTarget("file", jobLogTarget);
                FileTarget hostTarget = new FileTarget()
                {
                    FileName = Path.Combine(logDir, "Host", "Host.${date:yyyy-MM-dd-HHmm}.log")
                };
                ConfigureFileTarget(hostTarget);
                config.AddTarget("file", hostTarget);

                if (!inCloud)
                {
                    var consoleTarget = new SnazzyConsoleTarget();
                    config.AddTarget("console", consoleTarget);
                    consoleTarget.Layout = "[${logger:shortName=true}] ${message}";
                
                    LoggingRule allMessagesToConsole = new LoggingRule("*", NLog.LogLevel.Trace, consoleTarget);
                    config.LoggingRules.Add(allMessagesToConsole);
                }
                
                // All other rules transfer all kinds of log messages EXCEPT Trace.
                LoggingRule hostToFile = new LoggingRule("JobRunner", NLog.LogLevel.Debug, hostTarget);
                config.LoggingRules.Add(hostToFile);

                LoggingRule roleToFile = new LoggingRule("WorkerRole", NLog.LogLevel.Debug, hostTarget);
                config.LoggingRules.Add(roleToFile);

                LoggingRule jobLogs = new LoggingRule("Job.*", NLog.LogLevel.Debug, jobLogTarget);
                config.LoggingRules.Add(jobLogs);

                LogManager.Configuration = config;

                _logger = LogManager.GetLogger("WorkerRole");
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FatalError.txt"), ex.ToString());
            }

            _logger.Info("Logging Enabled to {0}", logDir);

            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    ConfigureAzureDiagnostics(logDir);
                }
                else
                {
                    _logger.Info("Skipping Azure Diagnostics, we aren't in Azure");
                }
            }
            catch (Exception ex)
            {
                _logger.InfoException("Skipping Azure Diagnostics, we got an exception trying to check if we are in Azure", ex);
            }

            try
            {
                _runner = LoadJobRunner(settings);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error loading Job Runner", ex);
            }
        }

        private void ConfigureAzureDiagnostics(string logDir)
        {
            var config = DiagnosticMonitor.GetDefaultInitialConfiguration();
            config.ConfigurationChangePollInterval = TimeSpan.FromMinutes(5);

            config.DiagnosticInfrastructureLogs.ScheduledTransferLogLevelFilter = Microsoft.WindowsAzure.Diagnostics.LogLevel.Verbose;
            config.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);
            config.DiagnosticInfrastructureLogs.BufferQuotaInMB = 100;

            var dumps = config.Directories.DataSources.Single(dir => String.Equals(dir.Container, "wad-crash-dumps"));
            config.Directories.BufferQuotaInMB = 2048;
            config.Directories.DataSources.Clear();
            config.Directories.DataSources.Add(dumps);
            config.Directories.DataSources.Add(new DirectoryConfiguration()
            {
                Container = "wad-joblogs",
                Path = Path.Combine(logDir, "Jobs"),
                DirectoryQuotaInMB = 100
            });
            config.Directories.DataSources.Add(new DirectoryConfiguration()
            {
                Container = "wad-hostlogs",
                Path = Path.Combine(logDir, "Host"),
                DirectoryQuotaInMB = 100
            });
            config.Directories.ScheduledTransferPeriod = TimeSpan.FromMinutes(1);

            config.WindowsEventLog.DataSources.Add("Application");
            config.WindowsEventLog.BufferQuotaInMB = 100;

            _logger.Info("Enabling Azure Diagnostics");
            DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", config);
            _logger.Info("Enabled Azure Diagnostics");
        }

        private static void ConfigureFileTarget(FileTarget hostTarget)
        {
            hostTarget.FileAttributes = Win32FileAttributes.WriteThrough;
            hostTarget.Layout = new JsonLayout();
            hostTarget.LineEnding = LineEndingMode.CRLF;
            hostTarget.Encoding = Encoding.UTF8;
            hostTarget.CreateDirs = true;
            hostTarget.EnableFileDelete = true;
            hostTarget.ArchiveEvery = FileArchivePeriod.Hour;
            hostTarget.ConcurrentWrites = false;
        }

        public override bool OnStart()
        {
            try
            {
                return _runner.OnStart();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during OnStart", ex);
                return false;
            }
        }

        public override void OnStop()
        {
            try
            {
                _runner.OnStop();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during OnStop", ex);
            }
        }

        public override void Run()
        {
            try
            {
                _runner.Run();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during Run", ex);
            }
        }

        public void RunSingleJob(string jobName)
        {
            try
            {
                _runner.RunSingleJob(jobName);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during RunSingleJob(" + jobName + ")", ex);
            }
        }

        public void RunSingleJobContinuously(string jobName)
        {
            try
            {
                _runner.RunSingleJobContinuously(jobName);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during RunSingleJobContinuously(" + jobName + ")", ex);
            }
        }

        public void Stop()
        {
            try
            {
                _runner.Stop();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during Stop", ex);
            }
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
            var worker = new WorkerRole(settings);

            // See which mode we're in
            if (String.IsNullOrWhiteSpace(jobName))
            {
                // Run ALL THE JOBS!
                worker.OnStart();
                worker.Run();
                Console.WriteLine("Worker is running. Press ENTER to stop");
                Console.ReadLine();
                worker.Stop();
                worker.OnStop();
            }
            else
            {
                // Run JUST ONE JOB!
                if (!continuous)
                {
                    worker.RunSingleJob(jobName);
                }
                else
                {
                    worker.RunSingleJobContinuously(jobName);
                }
            }
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