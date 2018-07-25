// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NuGet.Jobs;

namespace NuGet.SupportRequests.Notifications
{
    internal class Job
        : JobBase
    {
        private IServiceContainer _serviceContainer;
        private IDictionary<string, string> _jobArgsDictionary;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            if (!jobArgsDictionary.ContainsKey(JobArgumentNames.ScheduledTask))
            {
                throw new NotSupportedException("The required argument -Task is missing.");
            }

            _serviceContainer = serviceContainer ?? throw new ArgumentNullException(nameof(serviceContainer));
            _jobArgsDictionary = jobArgsDictionary;

            RegisterDatabase(serviceContainer, jobArgsDictionary, JobArgumentNames.SourceDatabase);
        }

        public Task<SqlConnection> OpenSupportSqlConnectionAsync()
        {
            return OpenSqlConnectionAsync(JobArgumentNames.SourceDatabase);
        }

        public override async Task Run()
        {
            var scheduledTask = ScheduledTaskFactory.Create(_serviceContainer, _jobArgsDictionary,
                OpenSupportSqlConnectionAsync, LoggerFactory);

            await scheduledTask.RunAsync();
        }
    }
}