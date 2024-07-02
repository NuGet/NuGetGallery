// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface ISimpleCloudBlob
    {
        ICloudBlobProperties Properties { get; }
        IDictionary<string, string> Metadata { get; }
        ICloudBlobCopyState CopyState { get; }
        Uri Uri { get; }
        string Name { get; }
        DateTime LastModifiedUtc { get; }
        string ETag { get; }
        bool IsSnapshot { get; }

        Task<Stream> OpenReadAsync(IAccessCondition accessCondition);
        Task<Stream> OpenWriteAsync(IAccessCondition accessCondition, string contentType = null);

        Task DeleteIfExistsAsync();
        Task DownloadToStreamAsync(Stream target);
        Task DownloadToStreamAsync(Stream target, IAccessCondition accessCondition);

        Task<bool> ExistsAsync();
        Task SetPropertiesAsync();
        Task SetPropertiesAsync(IAccessCondition accessCondition);
        Task SetMetadataAsync(IAccessCondition accessCondition);
        Task UploadFromStreamAsync(Stream source, bool overwrite);
        Task UploadFromStreamAsync(Stream source, IAccessCondition accessCondition);

        Task FetchAttributesAsync();

        Task StartCopyAsync(ISimpleCloudBlob source, IAccessCondition sourceAccessCondition, IAccessCondition destAccessCondition);

        /// <summary>
        /// Generates the shared access signature that if appended to the blob URI
        /// would allow actions matching the provided <paramref name="permissions"/> without having access to the
        /// access keys of the storage account.
        /// </summary>
        /// <param name="permissions">The permissions to include in the SAS token.</param>
        /// <param name="endOfAccess">
        /// "End of access" timestamp. After the specified timestamp, 
        /// the returned signature becomes invalid if implementation supports it.
        /// Null for no time limit.
        /// </param>
        /// <returns>Shared access signature in form of URI query portion.</returns>
        Task<string> GetSharedAccessSignature(FileUriPermissions permissions, DateTimeOffset endOfAccess);

        /// <summary>
        /// Opens the seekable read stream to the file in blob storage.
        /// </summary>
        /// <param name="serverTimeout">Timeout for a single HTTP request issued by implementation. See <see cref="BlobRequestOptions.ServerTimeout"/>.</param>
        /// <param name="maxExecutionTime">Total timeout accross all potential retries. See <see cref="BlobRequestOptions.MaximumExecutionTime"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Read stream for a blob in blob storage.</returns>
        Task<Stream> OpenReadStreamAsync(
            TimeSpan serverTimeout,
            CancellationToken cancellationToken);

        Task SnapshotAsync(CancellationToken token);

        /// <summary>
        /// Retrieves the blob contents as a string assuming UTF8 encoding if the blob exists.
        /// </summary>
        /// <returns>The text content of the blob or null if the blob does not exist.</returns>
        Task<string> DownloadTextIfExistsAsync();

        /// <summary>
        /// Calls <see cref="ISimpleCloudBlob.FetchAttributesAsync()"/> and determines if blob exists.
        /// </summary>
        /// <returns>True if <see cref="ISimpleCloudBlob.FetchAttributesAsync()"/> call succeeded, false if blob does not exist.</returns>
        Task<bool> FetchAttributesIfExistsAsync();

        /// <summary>
        /// Calls <see cref="ISimpleCloudBlob.OpenReadAsync(IAccessCondition)"/> without access condition and returns
        /// resulting stream if blob exists.
        /// </summary>
        /// <returns>Stream if the call was successful, null if blob does not exist.</returns>
        Task<Stream> OpenReadIfExistsAsync();
    }
}