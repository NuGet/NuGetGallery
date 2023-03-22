// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    internal static class TaskExtensions
    {
        public static async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> getTask, TimeSpan timeout)
        {
            // Source:
            // https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#using-a-timeout
            using (var cts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var mainTask = getTask(cts.Token);
                var resultTask = await Task.WhenAny(mainTask, delayTask);
                if (resultTask == delayTask)
                {
                    // Operation cancelled
                    throw new OperationCanceledException("The operation was forcibly canceled.");
                }
                else
                {
                    // Cancel the timer task so that it does not fire
                    cts.Cancel();
                }

                return await mainTask;
            }
        }
    }
}
