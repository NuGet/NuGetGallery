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
        /// <summary>
        /// Returns a max list of Uris.
        /// </summary>
        /// <param name="maxResults">The max number of files.</param>
        /// <param name="token">A cancellation token for the async operation.</param>
        /// <param name="prefix">A prefix for the files.</param>
        /// <returns>An <see cref="IEnumerable{Uri}"/></returns>
        Task<IEnumerable<Uri>> GetFilesAsync(int maxResults, CancellationToken token, string prefix = null);

        /// <summary>
        /// Clean - up action.
        /// This method will not throw an exception. 
        /// If an exception it will be stored under <see cref="AsyncOperationResult.OperationException"/>. 
        /// </summary>
        /// <param name="blobLock">The <see cref="AzureBlobLockResult"/> for the blob that needs clean-up.</param>
        /// <param name="onError">Flag to indicate if the cleanup is done because an error or not. </param>
        /// <param name="token">A token to be used for cancellation.</param>
        /// <returns>True is the cleanup was successful. If the blob does not exist the return value is false. 
        /// If an Exception is thrown the exception will be stored under <see cref="AsyncOperationResult.OperationException"/>.
        /// </returns>
        Task<AsyncOperationResult> TryCleanAsync(AzureBlobLockResult blobLock, bool onError, CancellationToken token);

        /// <summary>
        /// Returns a blob stream.
        /// </summary>
        /// <param name="blobUri">The uri.</param>
        /// <param name="contentType">The <see cref="ContentType"/></param>
        /// <param name="token">A cancellation token for the async operation.</param>
        /// <returns></returns>
        Task<Stream> OpenReadAsync(Uri blobUri, ContentType contentType, CancellationToken token);

        /// <summary>
        /// Take lease on a blob.
        /// </summary>
        /// <param name="blobUri">The blob uri.</param>
        /// <param name="token">The token for cancellation.</param>
        /// <returns>The result of the lock action. 
        /// If an Exception is thrown the exception will be stored under <see cref="AsyncOperationResult.OperationException"/>.
        /// </returns>
        Task<AzureBlobLockResult> TakeLockAsync(Uri blobUri, CancellationToken token);

        /// <summary>
        /// Release the lock.
        /// This method will not throw an exception. 
        /// If an exception it will be stored in <see cref="AsyncOperationResult.OperationException"/>. 
        /// </summary>
        /// <param name="blobLock">The <see cref="AzureBlobLockResult"/> for the blob that needs clean-up.</param>
        /// <param name="token">A cancellation token for the async operation.</param>
        /// <returns>The result of the operation.
        /// If an Exception is thrown the exception will be stored under <see cref="AsyncOperationResult.OperationException"/>.
        /// </returns>
        Task<AsyncOperationResult> TryReleaseLockAsync(AzureBlobLockResult blobLock, CancellationToken token);
    }
}
