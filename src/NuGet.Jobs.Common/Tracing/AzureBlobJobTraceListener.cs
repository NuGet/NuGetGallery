// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Jobs
{
    /// <summary>
    /// This TraceListener helps log to Azure Blob Storage
    /// NOTE: For logging within this class which is rare, try using LogConsoleOnly method
    ///       such that any logging from within this class does not hit storage
    /// </summary>
    public sealed class AzureBlobJobTraceListener
        : JobTraceListener
    {
        public const int MaxLogBatchSize = 100;

        private const int _maxExpectedLogsPerRun = 1000000;
        private const string _jobLogNameFormat = "{0}/{1}.txt";
        private const string _logStorageContainerName = "ng-jobs-logs";
        private const string _endedLogName = "ended";

        private static ConcurrentQueue<ConcurrentQueue<string>> _logQueues;
        private static ConcurrentQueue<string> _currentLogQueue;
        private static ConcurrentQueue<string> _localOnlyLogQueue;
        private static int _jobLogBatchCounter;
        private static int _jobQueueBatchCounter;
        private static int _lastFileNameCounter;

        private readonly CloudBlobContainer _logStorageContainer;
        private readonly string _jobLogNamePrefix;
        private readonly string _jobLocalLogFolderPath;

        // For example, if MaxExpectedLogsPerRun is a million, and MaxLogBatchSize is 100, then maximum possible log batch counter would be 10000
        // That is a maximum of 5 digits. So, leadingzero specifier string would be "00000"
        // (Actually, 5 digits can handle upto 99999 which is roughly 10 times the expected. We are being, roughly, 10 times lenient)
        private static readonly string JobLogBatchCounterLeadingZeroSpecifier =
            new String('0',
                1 + Convert.ToInt32(
                        Math.Ceiling(
                            Math.Log10(
                                _maxExpectedLogsPerRun / MaxLogBatchSize))));

        public AzureBlobJobTraceListener(string jobName, string primaryStorageAccount)
        {
            if (string.IsNullOrWhiteSpace(primaryStorageAccount))
            {
                throw new ArgumentException("Primary storage account connection string wasn't passed. Pass a valid azure storage connection string, or, pass '-consoleLogOnly' for avoiding this error");
            }

            string nugetJobsLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _logStorageContainerName);
            Directory.CreateDirectory(nugetJobsLocalPath);
            Interlocked.Exchange(ref _logQueues, new ConcurrentQueue<ConcurrentQueue<string>>());
            Interlocked.Exchange(ref _currentLogQueue, new ConcurrentQueue<string>());
            Interlocked.Exchange(ref _jobLogBatchCounter, 0);
            Interlocked.Exchange(ref _jobQueueBatchCounter, 0);
            Interlocked.Exchange(ref _localOnlyLogQueue, new ConcurrentQueue<string>());

            var logStorageAccount = CloudStorageAccount.Parse(primaryStorageAccount);
            _logStorageContainer = logStorageAccount.CreateCloudBlobClient().GetContainerReference(_logStorageContainerName);
            _logStorageContainer.CreateIfNotExists();

            var dt = DateTime.UtcNow;
            _jobLogNamePrefix = string.Format("{0}/{1}/{2}", jobName, dt.ToString("yyyy/MM/dd/HH/mm/ss/fffffff"), Environment.MachineName);
            _jobLocalLogFolderPath = Path.Combine(nugetJobsLocalPath, string.Format("{0}-{1}", jobName, dt.ToString("yyyy-MM-dd-HH-mm-ss-fffffff")));
            Directory.CreateDirectory(_jobLocalLogFolderPath);
            Task.Run(() => FlushRunner());
            Task.Run(() => LocalLogFlushRunner());
        }

        public override void Close()
        {
            base.Close();
            try
            {
                ConcurrentQueue<string> finalCurrentLogQueue;
                if (Equals(finalCurrentLogQueue = Interlocked.Exchange(ref _currentLogQueue, null), null))
                {
                    // CurrentLogQueue is already null. This means that the listener is closed
                    // Do nothing and just exit
                    LogConsoleOnly(TraceEventType.Warning, "AzureBlobJobTraceListener is already closed by another thread. Doing nothing");
                    return;
                }

                _logQueues.Enqueue(finalCurrentLogQueue);
                while (_jobLogBatchCounter != -1)
                {
                    // Don't use the other overload of base.Log with format and args. That will call back into this class
                    LogConsoleOnly(TraceEventType.Information, "Waiting for log flush runner loop to terminate...");
                    Thread.Sleep(2000);
                }

                if (!_logQueues.IsEmpty)
                {
                    throw new ArgumentException("LogQueues should be empty at the end of FlushAll...");
                }

                Interlocked.Exchange(ref _logQueues, null);
                var endedLogBlobName = string.Format(_jobLogNameFormat, _jobLogNamePrefix, _endedLogName);
                string lastFileName = string.Format(_jobLogNameFormat, _jobLogNamePrefix, _lastFileNameCounter.ToString(JobLogBatchCounterLeadingZeroSpecifier));
                Save(endedLogBlobName, lastFileName);

                while (!_localOnlyLogQueue.IsEmpty)
                {
                    // DO NOTHING
                    LogConsoleOnly(TraceEventType.Information, "Waiting for local logging flush runner loop to terminate...");
                    Thread.Sleep(2000);
                }
                LogConsoleOnly(TraceEventType.Information, "Setting Local log only queue to null");
                _localOnlyLogQueue = null;

                // At this point, the logs are all uploaded to azure and the current job run is done. Delete them
                if (Directory.Exists(_jobLocalLogFolderPath))
                {
                    LogConsoleOnly(TraceEventType.Information, "Deleting local log folder " + _jobLocalLogFolderPath);
                    Directory.Delete(_jobLocalLogFolderPath, recursive: true);
                }
                LogConsoleOnly(TraceEventType.Information, "Successfully completed flushing of logs");
            }
            catch (Exception ex)
            {
                LogConsoleOnly(TraceEventType.Error, "AzureBlobJobTraceListener.Close is crashing for unknown reason. Reporting error here without terminating the job and calling base.Close()");
                LogConsoleOnly(TraceEventType.Error, ex.ToString());
                LogConsoleOnly(TraceEventType.Information, "base.Close from AzureBlobJobTraceListener.Close is successful");
            }
        }

        private void QueueLog(string messageWithTraceLevel)
        {
            if (_currentLogQueue == null)
            {
                // Consider using IDisposable pattern
                throw new ArgumentException("CurrentLogQueue cannot be null. It is likely that FlushAll has been called");
            }

            // FlushAll should never get called until after all the logging is done
            // This method is not thread-safe. If there was multi threading, CurrentLogQueue even after this point could be null
            // Let NullReferenceException be thrown, if this happens
            if (_currentLogQueue.Count >= MaxLogBatchSize)
            {
                FlushCurrentQueue();
            }

            _localOnlyLogQueue.Enqueue(messageWithTraceLevel);
            _currentLogQueue.Enqueue(messageWithTraceLevel);
        }

        protected override void Log(TraceEventType traceEventType, string message)
        {
            LogConsoleOnly(traceEventType, message);

            var messageWithTraceEventType = MessageWithTraceEventType(traceEventType, message);
            QueueLog(messageWithTraceEventType);
        }

        protected override void Log(TraceEventType traceEventType, string format, params object[] args)
        {
            var message = string.Format(CultureInfo.InvariantCulture, format, args);
            Log(traceEventType, message);
        }

        protected override void Flush(bool skipCurrentBatch = false)
        {
            try
            {
                if (!skipCurrentBatch)
                {
                    FlushCurrentQueue();
                }

                ConcurrentQueue<string> headQueue;
                while (_logQueues.TryDequeue(out headQueue))
                {
                    string blobName = string.Format(_jobLogNameFormat, _jobLogNamePrefix, _jobLogBatchCounter.ToString(JobLogBatchCounterLeadingZeroSpecifier));
                    Save(blobName, headQueue);
                    Interlocked.Exchange(ref _lastFileNameCounter, _jobLogBatchCounter);
                    Interlocked.Increment(ref _jobLogBatchCounter);
                }
            }
            catch (Exception ex)
            {
                LogConsoleOnly(TraceEventType.Error, ex.ToString());
            }
        }

        private void FlushRunner()
        {
            Thread.Sleep(1000);
            while (_currentLogQueue != null)
            {
                Flush(skipCurrentBatch: true);
                Thread.Sleep(1000);
            }

            // Flush anything that may be pending due to timing
            Flush(skipCurrentBatch: true);

            // The following indicates that all the log flushing is done
            Interlocked.Exchange(ref _jobLogBatchCounter, -1);
        }

        private void LocalLogFlushRunner()
        {
            while (_localOnlyLogQueue != null)
            {
                LocalLogFlush();
            }
        }

        private void LocalLogFlush()
        {
            try
            {
                using (var writer = File.AppendText(GetJobLocalLogPath(_jobQueueBatchCounter)))
                {
                    string content;
                    while (_localOnlyLogQueue != null && _localOnlyLogQueue.TryDequeue(out content))
                    {
                        writer.WriteLine(content);
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // DO NOTHING
            }
        }

        private void FlushCurrentQueue()
        {
            LogConsoleOnly(TraceEventType.Verbose, "Creating a new concurrent log queue...");
            _logQueues.Enqueue(Interlocked.Exchange(ref _currentLogQueue, new ConcurrentQueue<string>()));
            Interlocked.Increment(ref _jobQueueBatchCounter);
        }

        private void Save(string blobName, ConcurrentQueue<string> eventMessages)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string eventMessage in eventMessages)
            {
                builder.AppendLine(eventMessage);
            }

            Save(blobName, builder.ToString());
        }

        private void Save(string blobName, string content)
        {
            // Don't use the other overload of base.Log with format and args. That will call back into this class
            var blob = _logStorageContainer.GetBlockBlobReference(blobName);
            LogConsoleOnly(TraceEventType.Verbose, "Uploading to " + blob.Uri);
            blob.UploadText(content);
        }

        private string GetJobLocalLogPath(int jobLogBatchCounter)
        {
            return Path.Combine(_jobLocalLogFolderPath, string.Format(CultureInfo.InvariantCulture, "{0}.txt", jobLogBatchCounter));
        }
    }
}
