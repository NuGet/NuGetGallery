// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// The result from <see cref="IRevalidationService.StartNextRevalidationAsync"/>
    /// </summary>
    public enum RevalidationResult
    {
        /// <summary>
        /// A revalidation was successfully enqueued.
        /// </summary>
        RevalidationEnqueued,

        /// <summary>
        /// A revalidation could not be enqueued at this time. The revalidation should be retried later.
        /// </summary>
        RetryLater,

        /// <summary>
        /// This instance of the revalidation job has reached an unrecoverable state and MUST stop.
        /// </summary>
        UnrecoverableError,
    }
}
