﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations.Tasks.Monitoring
{
    [Command("tailjoblog", "Show the last few entries from a job log and optionally polls for additional results", AltName = "tjl", MinArgs = 1, MaxArgs = 1)]
    public class TailJobLogTask : StorageTask
    {
        private DateTimeOffset _lastEntryUtc = DateTimeOffset.MinValue;

        [Option("The number of entries to retrieve from the log", AltName = "n")]
        public int? NumberOfEntries { get; set; }

        [Option("Set this switch to poll for additional log entries.", AltName = "f")]
        public bool Follow { get; set; }

        [Option("The time interval at which to poll for new job log entries", AltName = "p")]
        public TimeSpan? PollingPeriod { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            NumberOfEntries = NumberOfEntries ?? 10;
            PollingPeriod = PollingPeriod ?? TimeSpan.FromSeconds(5);
        }

        public override void ExecuteCommand()
        {
            var jobName = Arguments[0];

            // Start by fetching the latest log
            var joblogs = JobLog.LoadJobLogs(StorageAccount);

            // Grab the log
            var candidates = joblogs.Where(l => l.JobName.StartsWith(jobName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!candidates.Any())
            {
                Log.Error("No logs match: {0}", jobName);
            }
            else if (candidates.Count > 1)
            {
                Log.Error("Multiple logs match: {0}. Found: {1}", jobName, String.Join(", ", candidates.Select(c => c.JobName)));
            }
            else
            {
                // Grab the requested entries
                var log = candidates.Single();
                var entries = log.OrderedEntries().Take(NumberOfEntries.Value).Reverse();
                foreach (var entry in entries)
                {
                    WriteEntry(log, entry);
                    _lastEntryUtc = entry.Timestamp;
                }

                if (Follow)
                {
                    FollowLog(log);
                }
            }
        }

        private void FollowLog(JobLog log)
        {
            // Wait for PollingPeriod seconds
            Thread.Sleep(PollingPeriod.Value);

            // Grab new entries
            var entries = log.OrderedEntries().TakeWhile(l => l.Timestamp > _lastEntryUtc).Take(NumberOfEntries.Value);
            foreach (var entry in entries)
            {
                WriteEntry(log, entry);
                _lastEntryUtc = entry.Timestamp;
            }
        }

        private void WriteEntry(JobLog log, JobLogEntry entry)
        {
            Log.Log(entry.FullEvent);
        }
    }
}
