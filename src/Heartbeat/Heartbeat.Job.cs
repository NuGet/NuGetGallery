// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using NuGet.Jobs.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading;

namespace Heartbeat
{
    public class HeartbeatJob : JobBase
    {
        // Default configurations
        private const string DefaultConfig = "Heartbeat.config";
        private const string DefaultDashboardContainerName = "int0";

        // Job arguments

        private CloudStorageAccount DashboardStorage { get; set; }

        private CloudBlobContainer DashboardStorageContainer { get; set; }

        private string DashboardStorageContainerName { get; set; }


        // The initial day the job got kicked off on
        private int CurrentDay { get; set; }

        // Verbose log file for diagnostics
        private string VerboseLogFileName { get; set; }

        // Concise log file for daily reports on job health
        private string ConciseLogFileName { get; set; }

        //Alert log file for monitoring and sending Alerts
        private string AlertLogFileName { get; set; }

        private string LogFileSuffix { get; set; }

        private readonly Dictionary<string, int> JobsToMonitor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DateTime?> LastCheckedForJob = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, bool> JobSucceeded = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private EventLog AppEventLog;

        public HeartbeatJob()
        {
            CurrentDay = DateTime.UtcNow.Day;
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            string heartbeatconfig = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.HeartbeatConfig);

            if (String.IsNullOrEmpty(heartbeatconfig))
            {
                heartbeatconfig = DefaultConfig;
            }

            DashboardStorage = CloudStorageAccount.Parse(
                                        JobConfigManager.GetArgument(jobArgsDictionary,
                                            JobArgumentNames.DashboardStorageAccount, EnvironmentVariableKeys.StorageDashboard));

            DashboardStorageContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DashboardStorageContainer) ?? DefaultDashboardContainerName;


            DashboardStorageContainer = DashboardStorage.CreateCloudBlobClient().GetContainerReference(DashboardStorageContainerName);

            LogFileSuffix = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.LogFileSuffix);

            if (LogFileSuffix == null)
            {
                throw new InvalidOperationException("LogFileSuffix argument must be specified. EXAMPLE USAGE: -LogFileSuffix v2");
            }

            // Read Config File and get values
            ReadConfigFile(heartbeatconfig);

            foreach (var jobName in JobsToMonitor.Keys)
            {
                LastCheckedForJob[jobName] = null;
            }

            foreach (var jobName in JobsToMonitor.Keys)
            {
                JobSucceeded[jobName] = false;
            }

            // Initialize Log File names
            VerboseLogFileName = GenerateLogFileName("ProcessRecyle_Verbose_");
            ConciseLogFileName = GenerateLogFileName("ProcessRecyle_Concise_");
            AlertLogFileName = GenerateLogFileName("ProcessRecyle_Alert_");

            //Initialize Event Log
            AppEventLog = new EventLog();
            AppEventLog.Log = "Application";

            // Record process information
            foreach (var jobName in JobsToMonitor.Keys)
            {
                RecordProcessInformation(jobName, "START");
            }

            return true;
        }

        private string GenerateLogFileName(string fileNamePrefix)
        {
            return
                fileNamePrefix
                + LogFileSuffix
                + DateTime.UtcNow.Month.ToString()
                + CurrentDay.ToString()
                + ".txt";
        }

        public override async Task<bool> Run()
        {
            //Update Log File names
            if (CurrentDay != DateTime.UtcNow.Day)
            {
                CurrentDay = DateTime.UtcNow.Day;
                VerboseLogFileName = GenerateLogFileName("ProcessRecyle_Verbose_");
                ConciseLogFileName = GenerateLogFileName("ProcessRecyle_Concise_");
                AlertLogFileName = GenerateLogFileName("ProcessRecyle_Alert_");
            }

            var jobsKilled = new List<string>();

            //Check event log for each of the jobs
            foreach (var job in JobsToMonitor)
            {
                bool isTheJobRunning = IsTheJobRunning(job.Key, job.Value);

                if (!isTheJobRunning)
                {
                    jobsKilled.Add(job.Key);

                    // Kill the process and log to dashboard
                    KillService(job.Key);
                }
            }

            if (jobsKilled.Count > 0)
            {
                // give nssm time to restart the jobs.
                // TODO: Is this enough?
                Thread.Sleep(5000);

                foreach (var job in jobsKilled)
                {
                    // Record Restart info
                    RecordProcessInformation(job, "RESTART");
                }
            }

            return true;
        }

        private void RecordInStorage(string message, string fileName)
        {
            try
            {
                CloudBlockBlob logFile = DashboardStorageContainer.GetBlockBlobReference(fileName);
                string currentContents = string.Empty;
                if (logFile.Exists())
                {
                    currentContents = logFile.DownloadText();
                }

                logFile.UploadText(currentContents + message + "\r\n");
            }
            catch
            {
                // Fail if you cannot record in storage
                // This is okay because the monitoring will alert if the expected alerts aren't getting created
                // Also the secondary level monitoring and alerting will also let us know if there are issues with the processes
                Trace.TraceInformation(String.Format("FAILURE: RecordInStorage failed when trying to log message {0} to {1}", message, fileName));
            }
        }

        private void ReadConfigFile(String file)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException(String.Format("Specified config file {0} doesn't exist", file));
            }

            try
            {
                var lines = File.ReadAllLines(file);

                foreach (var line in lines)
                {
                    string[] values = (line ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length == 2)
                    {
                        try
                        {
                            var value = values[0].Trim();

                            JobsToMonitor.Add(value, Convert.ToInt32(values[1]));
                            Trace.TraceInformation(String.Format("Added {0} with threshhold {1} for monitoring.", value, values[1]));
                        }
                        catch (ArgumentException e)
                        {
                            // If there are duplicate entries or other issues, record in log and move on
                            RecordInStorage(String.Format("Duplicate exception {0} is thrown while adding Jobs to the dictionary.", e.Message), VerboseLogFileName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException(String.Format
                    ("Encounterd exception: {0}. USAGE: Config file should have comma separated job name and threshhold on separate lines for each of the jobs to be monitored.", e.Message));
            }
        }

        private bool IsTheJobRunning(string jobName, int threshhold)
        {
            //Skip checking if the current time is still within the window of threshhold from previous check and the job succeeded in the previous check
            if (JobSucceeded[jobName]) //This means that the job has been at least checked once and it succeeded the check
            {
                DateTime lastCheckedTime = LastCheckedForJob[jobName].Value;
                DateTime currentTime = DateTime.UtcNow;
                DateTime threshholdWindowTime = lastCheckedTime.AddMinutes(threshhold);
                if (currentTime < threshholdWindowTime)
                {
                    string message = String.Format("Job {0} was last checked at {1}. Current Time {2} is still less than {3}. So quitting without checking!",
                                        jobName, lastCheckedTime, currentTime, threshholdWindowTime);
                    Trace.TraceInformation(message);
                    RecordInStorage(message, VerboseLogFileName);
                    return true;
                }

            }

            //Time to get records from. Make this window based on the threshhold. 
            DateTime fromTime = DateTime.UtcNow.AddMinutes(threshhold * -1);

            // review sorting can be very expensive here if there are a lot of log entries.
            EventLogEntry lastEntry = AppEventLog.Entries
                                           .OfType<EventLogEntry>()
                                           .Where(c => c.TimeWritten.ToUniversalTime() >= fromTime)
                                           .Where(e => e.Source.ToString().Equals(jobName, StringComparison.OrdinalIgnoreCase))
                                           .LastOrDefault();

            // If there is no entries from this job, we are going to give it some time and retry getting logs.
            if (lastEntry == null)
            {
                if (!LastCheckedForJob[jobName].HasValue)
                {
                    LastCheckedForJob[jobName] = DateTime.UtcNow;
                }

                JobSucceeded[jobName] = false;

                string message = String.Format("Unable to retrieve tracing info for {0} at {1}", jobName, DateTime.UtcNow);

                RecordInStorage(message, VerboseLogFileName);
                Trace.TraceInformation(message);

                TimeSpan elapsedTimeWithoutEntires = DateTime.UtcNow.Subtract(LastCheckedForJob[jobName].Value);

                if (elapsedTimeWithoutEntires.TotalMinutes > threshhold)
                {
                    LastCheckedForJob[jobName] = null;

                    string retryMessage = String.Format("Killing process as we are unable to retrieve trace info {0} at {1}",
                        jobName,
                        DateTime.UtcNow);

                    RecordInStorage(retryMessage, VerboseLogFileName);
                    Trace.TraceInformation(retryMessage);

                    return false;
                }

                return true;
            }

            // If we found a value, then store the time it was checked and store that it succeeded
            JobSucceeded[jobName] = true;
            LastCheckedForJob[jobName] = DateTime.UtcNow;

            // All seems well since we had an entry for the job within the threshhold
            Trace.TraceInformation(String.Format("Job {0} is running successfully. Most recent timestamp is {1}", jobName, lastEntry.TimeWritten));

            return true;
        }

        private void RecordProcessInformation(string jobName, string action)
        {
            uint processId = GetProcessIdFromServiceName(jobName);
            Process process = null;

            try
            {
                process = Process.GetProcessById((int)processId);

                if (process != null)
                {
                    if (processId == 0)
                    {
                        string message = "Process {0} with Id {1} is STOPPED";

                        Trace.TraceInformation(String.Format(message, jobName, processId));

                        RecordInStorage(String.Format(message, action, jobName, processId), AlertLogFileName);
                        RecordInStorage(String.Format(message, action, jobName, processId), VerboseLogFileName);
                    }
                    else
                    {
                        string mesaage = "{0}: Process {1} with Id {2}";

                        Trace.TraceInformation(String.Format(mesaage, action, jobName, processId));

                        RecordInStorage(String.Format(mesaage, action, jobName, processId), VerboseLogFileName);
                    }
                }
                else
                {
                    RecordInStorage(String.Format("{0}: Process {1} with Id {2} is NULL ", action, jobName, processId), AlertLogFileName);
                    RecordInStorage(String.Format("{0}: Process {1} with Id {2} is NULL ", action, jobName, processId), VerboseLogFileName);
                }
            }
            catch (Exception e)
            {
                // Thrown if the process specified by processId is no longer running.

                string message = "{0}: Process {1} is not running. Failed with Exception {2}";

                Trace.TraceInformation(String.Format(message, action, jobName, e.Message));

                RecordInStorage(String.Format(message, action, jobName, e.Message), VerboseLogFileName);
                RecordInStorage(String.Format(message, action, jobName, e.Message), AlertLogFileName);

            }
        }

        public uint GetProcessIdFromServiceName(string serviceName)
        {
            uint processId = 0;
            string query = string.Format(
                 "SELECT ProcessId FROM Win32_Service WHERE Name='{0}'",
                 serviceName);

            ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(query);

            foreach (ManagementObject obj in searcher.Get())
            {
                processId = (uint)obj["ProcessId"];
            }
            return processId;
        }

        private bool KillService(string serviceName)
        {
            Trace.TraceInformation(String.Format("Trying to Kill Service {0} ", serviceName));
            uint processId = GetProcessIdFromServiceName(serviceName);

            //Find all child processes
            String myQuery = string.Format("SELECT * FROM WIN32_Process WHERE ParentProcessId={0}", processId);
            ObjectQuery objQuery = new ObjectQuery(myQuery);

            ManagementObjectSearcher objSearcher = new ManagementObjectSearcher(objQuery);
            ManagementObjectCollection processList = objSearcher.Get();

            // Kill all child processes
            foreach (ManagementObject item in processList)
            {
                uint pid = (uint)Convert.ToInt32(item["ProcessId"].ToString());
                KillProcess(pid, string.Empty);
            }

            bool result = KillProcess(processId, serviceName);
            return result;
        }

        private bool KillProcess(uint processId, string serviceName)
        {
            Process process = null;

            try
            {
                process = Process.GetProcessById((int)processId);
                if (String.IsNullOrEmpty(serviceName))
                {
                    serviceName = process.ProcessName;
                }
            }
            catch (ArgumentException)
            {
                // Thrown if the process specified by processId is no longer running.
                Trace.TraceInformation(String.Format("Process {0} is no longer running", serviceName));
                RecordInStorage(String.Format("Process {0} is no longer running", serviceName), VerboseLogFileName);
                return true;
            }

            try
            {
                if (process != null)
                {
                    Trace.TraceInformation(String.Format("Trying to Kill process {0} with process Id {1} ", serviceName, processId));
                    process.Kill();
                    process.Dispose();

                    Trace.TraceInformation(String.Format("Successfully killed process {0} with process Id {1} ", serviceName, processId));

                    RecordInStorage(String.Format("Successfully killed process {0} with process Id {1} at {2}", serviceName, processId, DateTime.UtcNow), ConciseLogFileName);
                    RecordInStorage(String.Format("Successfully killed process {0} with process Id {1} at {2}", serviceName, processId, DateTime.UtcNow), VerboseLogFileName);
                }
            }
            catch (Win32Exception)
            {
                // Thrown if process is already terminating,
                // the process is a Win16 exe or the process
                // could not be terminated.
                string message = "Process {0} is already terminating";

                Trace.TraceInformation(String.Format(message, serviceName));

                RecordInStorage(String.Format(message, serviceName), VerboseLogFileName);
                RecordInStorage(String.Format(message, serviceName), AlertLogFileName);

                return false;
            }
            catch (InvalidOperationException)
            {
                // Thrown if the process has already terminated.
                string message = "Process {0} is has already terminated";

                Trace.TraceInformation(String.Format(message, serviceName));

                RecordInStorage(String.Format(message, serviceName), VerboseLogFileName);
                RecordInStorage(String.Format(message, serviceName), AlertLogFileName);

                return false;
            }

            return true;
        }
    }
}
