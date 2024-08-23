// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// The state shared between the Gallery and the revalidation job.
    /// </summary>
    public interface IRevalidationJobStateService
    {
        /// <summary>
        /// Check whether the revalidation state has been initialized.
        /// </summary>
        /// <returns>Whether the revalidation state has been initialized.</returns>
        Task<bool> IsInitializedAsync();

        /// <summary>
        /// Update the settings to mark the revalidation job as initialized.
        /// </summary>
        /// <returns>A task that completes once the settings have been updated.</returns>
        Task MarkAsInitializedAsync();

        /// <summary>
        /// Check whether the killswitch has been activated. If it has, all revalidation operations should be halted.
        /// </summary>
        /// <returns>Whether the killswitch has been activated.</returns>
        Task<bool> IsKillswitchActiveAsync();

        /// <summary>
        /// Determine the desired package event rate per hour. Package events include package pushes,
        /// edits, and revalidations.
        /// </summary>
        /// <returns>The desired maximum number of package events per hour.</returns>
        Task<int> GetDesiredPackageEventRateAsync();

        /// <summary>
        /// Reset the desired package event rate to the configured minimum.
        /// </summary>
        /// <returns>A task that completes once the rate has been reset.</returns>
        Task ResetDesiredPackageEventRateAsync();

        /// <summary>
        /// Increment the desired package event rate per hour.
        /// </summary>
        /// <returns>A task that completes once the rate has been incremented.</returns>
        Task IncreaseDesiredPackageEventRateAsync();
    }
}
