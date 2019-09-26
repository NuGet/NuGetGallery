// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <remarks>
    /// Can (and probably should) be replaced with Polly library if the project is updated to target .netfx 4.7.2.
    /// In current state Polly pulls a ton of System.* dependencies which we previously didn't have.
    /// </remarks>
    /// <seealso cref="NuGet.Indexing.Retry"/>
    public class Retry
    {
        /// <summary>
        /// Retries async operation if it throws with delays between attempts.
        /// </summary>
        /// <param name="runLogicAsync">Operation to try.</param>
        /// <param name="shouldRetryOnException">Exception predicate. If it returns false, the exception will propagate to the caller.</param>
        /// <param name="maxRetries">Max number of attempts to make.</param>
        /// <param name="initialWaitInterval">Delay after the first failure.</param>
        /// <param name="waitIncrement">Delay increment for subsequent attempts.</param>
        public static async Task IncrementalAsync(
            Func<Task> runLogicAsync,
            Func<Exception, bool> shouldRetryOnException,
            int maxRetries,
            TimeSpan initialWaitInterval,
            TimeSpan waitIncrement)
        {
            for (int currentRetry = 0; currentRetry < maxRetries; ++currentRetry)
            {
                try
                {
                    await runLogicAsync();
                    return;
                }
                catch (Exception e) when (currentRetry < maxRetries - 1 && shouldRetryOnException(e))
                {
                    await Task.Delay(initialWaitInterval + TimeSpan.FromSeconds(waitIncrement.TotalSeconds * currentRetry));
                }
            }
        }

        /// <summary>
        /// Retries async operation if it throws or returns certain result with delays between attempts.
        /// </summary>
        /// <typeparam name="TResult">Attempted operation result type.</typeparam>
        /// <param name="runLogicAsync">Operation to try.</param>
        /// <param name="shouldRetryOnException">Exception predicate. If it returns false, the exception will propagate to the caller.</param>
        /// <param name="shouldRetry">Result predicate. If returns true, the result will be discarded and operation retried.</param>
        /// <param name="maxRetries">Max number of attempts to make.</param>
        /// <param name="initialWaitInterval">Delay after the first failure.</param>
        /// <param name="waitIncrement">Delay increment for subsequent attempts.</param>
        /// <returns>The result of <paramref name="runLogicAsync"/>() call if <paramref name="shouldRetry"/> predicate fails.
        /// default(<typeparamref name="TResult"/>) if <paramref name="shouldRetry"/> suceeded for all attempts.</returns>
        public static async Task<TResult> IncrementalAsync<TResult>(
            Func<Task<TResult>> runLogicAsync,
            Func<Exception, bool> shouldRetryOnException,
            Func<TResult, bool> shouldRetry,
            int maxRetries,
            TimeSpan initialWaitInterval,
            TimeSpan waitIncrement)
        {
            var result = default(TResult);
            for (int currentRetry = 0; currentRetry < maxRetries; ++currentRetry)
            {
                try
                {
                    result = await runLogicAsync();
                    if (!shouldRetry(result))
                    {
                        return result;
                    }
                }
                catch (Exception e) when (currentRetry < maxRetries - 1 && shouldRetryOnException(e))
                {
                    await Task.Delay(initialWaitInterval + TimeSpan.FromSeconds(waitIncrement.TotalSeconds * currentRetry));
                }
            }

            return result;
        }
    }
}
