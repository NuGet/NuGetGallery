// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Threading;
using NuGet.Services.Configuration;

namespace Ng.Jobs
{
    public abstract class LoopingNgJob : NgJob
    {
        protected LoopingNgJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override async Task Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var intervalSec = arguments.GetOrDefault(Arguments.Interval, Arguments.DefaultInterval);
            Logger.LogInformation("Looping job at interval {Interval} seconds.", intervalSec);

            // It can be expensive to initialize, so don't initialize on every run.
            // Remember the last time we initialized, and only reinitialize if a specified interval has passed since then.
            DateTime? timeLastInitialized = null;
            do
            {
                var timeMustReinitialize = DateTime.UtcNow.Subtract(new TimeSpan(0, 0, 0,
                    arguments.GetOrDefault(Arguments.ReinitializeIntervalSec, Arguments.DefaultReinitializeIntervalSec)));

                if (!timeLastInitialized.HasValue || timeLastInitialized.Value <= timeMustReinitialize)
                {
                    Logger.LogInformation("Initializing job.");
                    Init(arguments, cancellationToken);
                    timeLastInitialized = timeMustReinitialize;
                }

                Logger.LogInformation("Running job.");
                await RunInternal(cancellationToken);
                Logger.LogInformation("Job finished!");
                Logger.LogInformation("Waiting {Interval} seconds before starting job again.", intervalSec);
                await Task.Delay(intervalSec * 1000, cancellationToken);
            } while (true);
        }
    }
}
