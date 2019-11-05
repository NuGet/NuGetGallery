// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Extensions.Tables;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private readonly CloudBlockBlob _blob;

        public BlobProperties Properties => _blob.Properties;
        public IDictionary<string, string> Metadata => _blob.Metadata;
        public CopyState CopyState => _blob.CopyState;
        public Uri Uri => _blob.Uri;
        public string Name => _blob.Name;
        public DateTime LastModifiedUtc => _blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue;
        public string ETag => _blob.Properties.ETag;
        public bool IsSnapshot => _blob.IsSnapshot;

        public CloudBlobWrapper(CloudBlockBlob blob)
        {
            _blob = blob;
        }

        public static CloudBlobWrapper FromUri(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (!IsBlobStorageUri(uri))
            {
                throw new ArgumentException($"{nameof(uri)} must point to blob storage", nameof(uri));
            }

            var blob = new CloudBlockBlob(uri);
            return new CloudBlobWrapper(blob);
        }

        public async Task<Stream> OpenReadAsync(AccessCondition accessCondition)
        {
            return await _blob.OpenReadAsync(
                accessCondition: accessCondition,
                options: null,
                operationContext: null);
        }

        public async Task<Stream> OpenWriteAsync(AccessCondition accessCondition)
        {
            return await _blob.OpenWriteAsync(
                accessCondition: accessCondition,
                options: null,
                operationContext: null);
        }

        public async Task DeleteIfExistsAsync()
        {
            await _blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                accessCondition: null,
                options: null,
                operationContext: null);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return DownloadToStreamAsync(target, accessCondition: null);
        }

        public async Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition)
        {
            // Note: Overloads of FromAsync that take an AsyncCallback and State to pass through are more efficient:
            //  http://blogs.msdn.com/b/pfxteam/archive/2009/06/09/9716439.aspx
            var options = new BlobRequestOptions()
            {
                // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
                RetryPolicy = new DontRetryOnNotModifiedPolicy(new LinearRetry())
            };

            await _blob.DownloadToStreamAsync(target, accessCondition, options, operationContext: null);
        }

        public async Task<bool> ExistsAsync()
        {
            return await _blob.ExistsAsync();
        }

        public async Task SnapshotAsync(CancellationToken token)
        {
            await _blob.SnapshotAsync(token);
        }

        public async Task SetPropertiesAsync()
        {
            await _blob.SetPropertiesAsync();
        }

        public async Task SetPropertiesAsync(AccessCondition accessCondition)
        {
            await _blob.SetPropertiesAsync(accessCondition, options: null, operationContext: null);
        }

        public async Task SetMetadataAsync(AccessCondition accessCondition)
        {
            await _blob.SetMetadataAsync(accessCondition, options: null, operationContext: null);
        }

        public async Task UploadFromStreamAsync(Stream source, bool overwrite)
        {
            if (overwrite)
            {
                await _blob.UploadFromStreamAsync(source);
            }
            else
            {
                await UploadFromStreamAsync(source, AccessCondition.GenerateIfNoneMatchCondition("*"));
            }
        }

        public async Task UploadFromStreamAsync(Stream source, AccessCondition accessCondition)
        {
            await _blob.UploadFromStreamAsync(
                source,
                accessCondition,
                options: null,
                operationContext: null);
        }

        public async Task FetchAttributesAsync()
        {
            await _blob.FetchAttributesAsync();
        }

        public string GetSharedAccessSignature(SharedAccessBlobPermissions permissions, DateTimeOffset? endOfAccess)
        {
            var accessPolicy = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = endOfAccess,
                Permissions = permissions,
            };

            var signature = _blob.GetSharedAccessSignature(accessPolicy);

            return signature;
        }

        public async Task StartCopyAsync(ISimpleCloudBlob source, AccessCondition sourceAccessCondition, AccessCondition destAccessCondition)
        {
            // To avoid this we would need to somehow abstract away the primary and secondary storage locations. This
            // is not worth the effort right now!
            var sourceWrapper = source as CloudBlobWrapper;
            if (sourceWrapper == null)
            {
                throw new ArgumentException($"The source blob must be a {nameof(CloudBlobWrapper)}.");
            }

            await _blob.StartCopyAsync(
                sourceWrapper._blob,
                sourceAccessCondition: sourceAccessCondition,
                destAccessCondition: destAccessCondition,
                options: null,
                operationContext: null);
        }

        public async Task<Stream> OpenReadStreamAsync(
            TimeSpan serverTimeout,
            TimeSpan maxExecutionTime,
            CancellationToken cancellationToken)
        {
            var accessCondition = AccessCondition.GenerateEmptyCondition();
            var blobRequestOptions = new BlobRequestOptions
            {
                ServerTimeout = serverTimeout,
                MaximumExecutionTime = maxExecutionTime,
                RetryPolicy = new ExponentialRetry(),
            };
            var operationContext = new OperationContext();

            return await _blob.OpenReadAsync(accessCondition, blobRequestOptions, operationContext, cancellationToken);
        }

        private static bool IsBlobStorageUri(Uri uri)
        {
            return uri.Authority.EndsWith(".blob.core.windows.net");
        }

        // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
        private class DontRetryOnNotModifiedPolicy : IRetryPolicy
        {
            private IRetryPolicy _innerPolicy;

            public DontRetryOnNotModifiedPolicy(IRetryPolicy policy)
            {
                _innerPolicy = policy;
            }

            public IRetryPolicy CreateInstance()
            {
                return new DontRetryOnNotModifiedPolicy(_innerPolicy.CreateInstance());
            }

            public bool ShouldRetry(int currentRetryCount, int statusCode, Exception lastException, out TimeSpan retryInterval, OperationContext operationContext)
            {
                if (statusCode == (int)HttpStatusCode.NotModified)
                {
                    retryInterval = TimeSpan.Zero;
                    return false;
                }
                else
                {
                    return _innerPolicy.ShouldRetry(currentRetryCount, statusCode, lastException, out retryInterval, operationContext);
                }
            }
        }
    }
}