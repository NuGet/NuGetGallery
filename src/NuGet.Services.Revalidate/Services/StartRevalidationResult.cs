// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    /// <summary>
    /// The result of <see cref="IRevalidationStarter.StartNextRevalidationsAsync"/>.
    /// </summary>
    public class StartRevalidationResult
    {
        /// <summary>
        /// A revalidation could not be enqueued at this time. The revalidations should be retried later.
        /// </summary>
        public static readonly StartRevalidationResult RetryLater = new StartRevalidationResult(StartRevalidationStatus.RetryLater);

        /// <summary>
        /// This instance of the revalidation job has reached an unrecoverable state and MUST stop.
        /// </summary>
        public static readonly StartRevalidationResult UnrecoverableError = new StartRevalidationResult(StartRevalidationStatus.UnrecoverableError);

        private StartRevalidationResult(StartRevalidationStatus status, int revalidationsStarted = 0)
        {
            if (revalidationsStarted < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revalidationsStarted));
            }

            Status = status;
            RevalidationsStarted = revalidationsStarted;
        }

        /// <summary>
        /// Constructs a result that indicates one or more revalidations were successfully enqueued.
        /// </summary>
        /// <param name="revalidationsStarted">The number of revalidations that were enqueued.</param>
        /// <returns>The constructed revalidation result.</returns>
        public static StartRevalidationResult RevalidationsEnqueued(int revalidationsStarted)
        {
            return new StartRevalidationResult(StartRevalidationStatus.RevalidationsEnqueued, revalidationsStarted);
        }

        /// <summary>
        /// The status of starting revalidations.
        /// </summary>
        public StartRevalidationStatus Status { get; }

        /// <summary>
        /// The number of revalidations that were started.
        /// </summary>
        public int RevalidationsStarted { get; }
    }
}
