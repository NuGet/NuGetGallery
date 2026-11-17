// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Coordinates promotion work across instances using a renewable distributed lease.
    /// </summary>
    public interface IStagingPromotionLease
    {
        /// <summary>
        /// Runs work exclusively for the resource, or returns false when another worker holds its lease.
        /// </summary>
        Task<bool> RunAsync(string resourceName, Func<Task<bool>> process);

        /// <summary>
        /// Verifies and renews ownership before advancing publication.
        /// </summary>
        Task EnsureHeldAsync();
    }
}
