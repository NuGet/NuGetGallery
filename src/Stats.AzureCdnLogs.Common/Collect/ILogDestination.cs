// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public interface ILogDestination
    {
        /// <summary>
        /// Write to the destination blob. If the destinatio blob already exists the result will be false.
        /// This method will not throw an exception. 
        /// Use the <see cref="AsyncOperationResult.OperationException"/> to query for any exception during this execution.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="writeAction">The transfer action.</param>
        /// <param name="destinationFileName">The destination file name.</param>
        /// <param name="destinationContentType">The destination <see cref="ContentType"/>.</param>
        /// <param name="token">A cancellation token for the async operation.</param>
        /// <returns>The <see cref="AsyncOperationResult"/>. If an Exception is thrown the exception will be stored under <see cref="AsyncOperationResult.OperationException"/>.</returns>
        Task<AsyncOperationResult> TryWriteAsync(Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token);
    }
}
