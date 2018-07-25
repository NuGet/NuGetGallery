// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public interface IRevalidationThrottler
    {
        /// <summary>
        /// Check whether the revalidation capacity has been reached.
        /// </summary>
        /// <returns>If true, no more revalidations should be performed.</returns>
        Task<bool> IsThrottledAsync();

        /// <summary>
        /// Delay the current task to achieve the desired revalidation rate.
        /// </summary>
        /// <returns>Delay the task to ensure the desired revalidation rate.</returns>
        Task DelayUntilNextRevalidationAsync();

        /// <summary>
        /// Delay the current task until when a revalidation can be retried.
        /// </summary>
        /// <returns>Delay the task until when revalidations can be retried.</returns>
        Task DelayUntilRevalidationRetryAsync();
    }
}
