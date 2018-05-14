// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs
{
    public abstract class JobBase
    {
        private readonly EventSource _jobEventSource;

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

        protected ILoggerFactory LoggerFactory { get; private set; }

        protected ILogger Logger { get; private set; }

        public void SetLogger(ILoggerFactory loggerFactory, ILogger logger)
        {
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        public abstract void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary);

        public abstract Task Run();
    }
}
