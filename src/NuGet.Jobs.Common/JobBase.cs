// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    public abstract class JobBase
    {
        private readonly EventSource _jobEventSource;
        private JobTraceEventListener _jobTraceEventListener;

        protected JobBase()
            : this(null)
        {
        }

        protected JobBase(EventSource jobEventSource)
        {
            JobName = GetType().ToString();
            _jobEventSource = jobEventSource;
        }

        public string JobName { get; private set; }

        public JobTraceListener JobTraceListener { get; private set; }

        public void SetJobTraceListener(JobTraceListener jobTraceListener)
        {
            JobTraceListener = jobTraceListener;
            Trace.Listeners.Add(jobTraceListener);

            if (_jobTraceEventListener != null)
            {
                _jobTraceEventListener.Dispose();
            }

            if (_jobEventSource != null)
            {
                _jobTraceEventListener = new JobTraceEventListener(JobTraceListener);
                _jobTraceEventListener.EnableEvents(_jobEventSource, EventLevel.LogAlways);
            }
        }

        public abstract bool Init(IDictionary<string, string> jobArgsDictionary);

        public abstract Task<bool> Run();
    }
}
