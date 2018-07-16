// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Revalidate
{
    public class RevalidationQueueConfiguration
    {
        /// <summary>
        /// The maximum times that the <see cref="RevalidationQueue"/> should look for a revalidation
        /// before giving up.
        /// </summary>
        public int MaximumAttempts { get; set; } = 5;

        /// <summary>
        /// The time to sleep after an initialized revalidation is deemed completed.
        /// </summary>
        public TimeSpan SleepBetweenAttempts { get; set; } = TimeSpan.FromSeconds(5);
    }
}
