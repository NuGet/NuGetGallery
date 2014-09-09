using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public sealed class AzureBlobJobTraceLogger : JobTraceLogger
    {
        // 'const's
        private const int MaxQueueSize = 1000;
        private const string JobLogNameFormat = "{0}-{1}.txt";
        private const string LogStorageContainerName = "ng-jobs-logs";
        private const string MessageWithTraceLevelFormat = "[{0}]:{1}";

        // Static members
        private readonly static ConcurrentQueue<ConcurrentQueue<string>> LogQueues = new ConcurrentQueue<ConcurrentQueue<string>>();
        private static ConcurrentQueue<string> CurrentLogQueue = new ConcurrentQueue<string>();
        private static int JobLogBatchCounter = 1;

        // Instance members
        private CloudStorageAccount LogStorageAccount { get; set; }
        private CloudBlobContainer LogStorageContainer { get; set; }
        private string JobLogNamePrefix { get; set; }

        public AzureBlobJobTraceLogger(string logName) : base(logName)
        {
            var cstr = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.StoragePrimary);
            if(cstr == null)
            {
                throw new ArgumentException("Storage Primary environment variable needs to be set to use Azure Storage for logging. Try passing '-consoleLogOnly' for avoiding this error");
            }

            LogStorageAccount = CloudStorageAccount.Parse(cstr);
            LogStorageContainer = LogStorageAccount.CreateCloudBlobClient().GetContainerReference(LogStorageContainerName);
            LogStorageContainer.CreateIfNotExists();

            JobLogNamePrefix = String.Format("{0}-{1}", DateTime.UtcNow.ToString("yyyy-MM-dd-hh"), LogPrefix);

            Task.Run(() => FlushRunner());
        }

        public override void Log(TraceLevel traceLevel, string message)
        {
            // Don't use the other overload of base.Log with format and args. That will call back into this class
            base.Log(traceLevel, message);
            QueueLog(
                GetFormattedMessage(
                    MessageWithTraceLevel(
                        traceLevel,
                        message)));
        }

        public override void Log(TraceLevel traceLevel, string format, params object[] args)
        {
            var message = String.Format(format, args);
            this.Log(traceLevel, message);
        }

        private string MessageWithTraceLevel(TraceLevel traceLevel, string message)
        {
            return String.Format(MessageWithTraceLevelFormat, traceLevel.ToString(), message);
        }

        private void QueueLog(string messageWithTraceLevel)
        {
            if(CurrentLogQueue == null)
            {
                // Consider using IDisposable pattern
                throw new ArgumentException("CurrentLogQueue cannot be null. It is likely that FlushAll has been called");
            }
            if(CurrentLogQueue.Count >= MaxQueueSize)
            {
                // Don't use the other overload of base.Log with format and args. That will call back into this class
                base.Log(TraceLevel.Warning, "Creating a new concurrent log queue...");
                LogQueues.Enqueue(CurrentLogQueue);
                CurrentLogQueue = new ConcurrentQueue<string>();
            }

            CurrentLogQueue.Enqueue(messageWithTraceLevel);
        }

        public void FlushRunner()
        {
            Thread.Sleep(1000);
            while (CurrentLogQueue != null)
            {
                Flush();
                Thread.Sleep(1000);
            }

            // Flush anything that may be pending due to timing
            Flush();

            // The following indicates that all the log flushing is done
            Interlocked.Exchange(ref JobLogBatchCounter, -1);
        }

        private void Flush()
        {
            try
            {
                ConcurrentQueue<string> headQueue;
                while (LogQueues.TryDequeue(out headQueue))
                {
                    string blobName = String.Format(JobLogNameFormat, JobLogNamePrefix, JobLogBatchCounter);
                    // Don't use the other overload of base.Log with format and args. That will call back into this class
                    base.Log(TraceLevel.Warning, "Saving " + blobName);
                    Save(blobName, headQueue);
                    Interlocked.Increment(ref JobLogBatchCounter);
                }
            }
            catch (Exception ex)
            {
                this.Log(TraceLevel.Error, ex.ToString());
            }
        }

        public override void FlushAll()
        {
            base.FlushAll();
            LogQueues.Enqueue(CurrentLogQueue);
            CurrentLogQueue = null;

            while(JobLogBatchCounter != -1)
            {
                // Don't use the other overload of base.Log with format and args. That will call back into this class
                base.Log(TraceLevel.Warning, "Waiting for flush runner loop to terminate...");
                Thread.Sleep(2000);
            }
        }

        private void Save(string blobName, ConcurrentQueue<string> eventMessages)
        {
            StringBuilder builder = new StringBuilder();
            foreach(string eventMessage in eventMessages)
            {
                builder.AppendLine(eventMessage);
            }

            var blob = LogStorageContainer.GetBlockBlobReference(blobName);
            blob.UploadText(builder.ToString());
        }
    }
}
