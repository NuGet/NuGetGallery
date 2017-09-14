// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public interface ILogSource
    {
        Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token);

        Task<bool> CleanAsync(Uri fileUri, bool onError, CancellationToken token);

        Task<Stream> OpenReadAsync(Uri fileUri, ContentType contentType, CancellationToken token);

        /// <summary>
        /// Take lock.
        /// </summary>
        /// <param name="fileUri">The file uri.</param>
        /// <param name="token">The token for cancellation.</param>
        /// <returns>The status of the operaton and a task that will continue taking the lock overtime.</returns>
        Task<Tuple<bool, Task>> TakeLockAsync(Uri fileUri, CancellationToken token);

        Task<bool> ReleaseLockAsync(Uri fileUri, CancellationToken token);
    }
}
