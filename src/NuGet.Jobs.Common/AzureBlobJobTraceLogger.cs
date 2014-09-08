using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NuGet.Jobs.Common
{
    internal class JobTraceEvent
    {
        public JobTraceEvent(TraceLevel tracelevel, string message)
        {
            if(String.IsNullOrEmpty(message))
            {
                throw new ArgumentNullException("message");
            }
            TraceLevel = tracelevel;
            Message = message;
        }
        public TraceLevel TraceLevel {get; private set;}
        public string Message {get; private set;}
    }
    public sealed class AzureBlobJobTraceLogger : JobTraceLogger
    {
        private readonly ConcurrentQueue<ConcurrentQueue<JobTraceEvent>> LogQueues = new ConcurrentQueue<ConcurrentQueue<JobTraceEvent>>();
        private ConcurrentQueue<JobTraceEvent> CurrentLogQueue;
        private const int MaxQueueSize = 1000;

        private readonly CloudStorageAccount LogStorageAccount;
        private const string JobContainerName = "nugetjobs";

        public AzureBlobJobTraceLogger(string logName) : base(logName)
        {
            CurrentLogQueue = new ConcurrentQueue<JobTraceEvent>();
            var cstr = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.StoragePrimary);
            if(cstr == null)
            {
                throw new ArgumentException("Storage Primary environment variable needs to be set to use Azure Storage for logging. Try passing '-consoleLogOnly' for avoiding this error");
            }

            LogStorageAccount = CloudStorageAccount.Parse(cstr);
        }

        public override void Log(TraceLevel traceLevel, string message)
        {
            base.Log(traceLevel, message);
            QueueLog(traceLevel, GetFormattedMessage(message));
        }

        public override void Log(TraceLevel traceLevel, string format, params object[] args)
        {
            var message = String.Format(format, args);
            this.Log(traceLevel, message);
        }

        private void QueueLog(TraceLevel traceLevel, string message)
        {
            if(CurrentLogQueue == null)
            {
                // Consider using IDisposable pattern
                throw new ArgumentException("CurrentLogQueue cannot be null. It is likely that FlushAll has been called");
            }
            if(CurrentLogQueue.Count >= MaxQueueSize)
            {
                Console.WriteLine("Creating a new concurrent log queue...");
                LogQueues.Enqueue(CurrentLogQueue);
                CurrentLogQueue = new ConcurrentQueue<JobTraceEvent>();
            }

            CurrentLogQueue.Enqueue(new JobTraceEvent(traceLevel, message));
        }

        public override void Flush()
        {
            base.Flush();
        }

        public override void FlushAll()
        {
            base.FlushAll();
            LogQueues.Enqueue(CurrentLogQueue);
            CurrentLogQueue = null;
        }
    }
}
