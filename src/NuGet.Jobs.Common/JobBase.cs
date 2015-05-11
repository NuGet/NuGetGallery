// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public abstract class JobBase
    {
        private EventSource JobEventSource { get; set; }
        public JobBase() : this(null) { }
        public JobBase(EventSource jobEventSource)
        {
            JobName = this.GetType().ToString();
            JobEventSource = jobEventSource;
        }

        public string JobName { get; protected set; }

        public JobTraceListener JobTraceListener { get; protected set; }

        private JobTraceEventListener JobTraceEventListener { get; set; }

        public void SetJobTraceListener(JobTraceListener jobTraceListener)
        {
            JobTraceListener = jobTraceListener;
            Trace.Listeners.Add(jobTraceListener);

            if(JobTraceEventListener != null)
            {
                JobTraceEventListener.Dispose();
            }

            if(JobEventSource != null)
            {
                JobTraceEventListener = new JobTraceEventListener(JobTraceListener);
                JobTraceEventListener.EnableEvents(JobEventSource, EventLevel.LogAlways);
            }
        }

        public abstract bool Init(IDictionary<string, string> jobArgsDictionary);

        public abstract Task<bool> Run();
    }
}
