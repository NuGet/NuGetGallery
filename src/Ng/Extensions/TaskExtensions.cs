// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ng.Extensions
{
    public static class TaskExtensions
    {
        // From: https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md#cancelling-uncancellable-operations
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, cts.Token);

                var resultTask = await Task.WhenAny(task, delayTask);
                if (resultTask == delayTask)
                {
                    // Operation cancelled
                    throw new OperationCanceledException();
                }
                else
                {
                    // Cancel the timer task so that it does not fire
                    cts.Cancel();
                }

                await task;
            }
        }
    }
}
