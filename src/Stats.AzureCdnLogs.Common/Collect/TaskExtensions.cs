// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Creates a continuation Task with cancellation token attached to it. If the task passed as <paramref name="task"/> still
        /// runs when cancellation token fires it will throw <see cref="TaskCanceledException"/>. It will not do anything about
        /// the original task, but it will give the execution back to thee caller, so it can handle the cancellation.
        /// </summary>
        /// <param name="task">Original task to "inject" cancellation into.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that will throw if cancellation token fires before the original task completes.</returns>
        /// <exception cref="TaskCanceledException">Thrown when cancellation happens before <paramref name="task"/> completes.</exception>
        /// <remarks>
        /// https://stackoverflow.com/a/28626769/31782
        /// 
        /// This is useful for tasks that themselves do not handle cancellation well (or at all).
        /// </remarks>
        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            return task.IsCompleted
                ? task
                : task.ContinueWith(
                    completedTask => completedTask.GetAwaiter().GetResult(),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
    }
}
