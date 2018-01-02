// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// DI helper that wraps <see cref="CancellationToken"/>
    /// </summary>
    public interface IShutdownNotificationTokenProvider
    {

        /// <summary>
        /// Cancellation token the gets signaled about initiated shutdown
        /// </summary>
        CancellationToken Token { get; }
    }
}
