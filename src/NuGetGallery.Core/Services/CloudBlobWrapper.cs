// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private readonly CloudBlockBlob _blob;

        public CloudBlobWrapper(CloudBlockBlob blob)
        {
            _blob = blob;
        }

        public BlobProperties Properties
        {
            get { return _blob.Properties; }
        }

        public CopyState CopyState
        {
            get { return _blob.CopyState; }
        }

        public Uri Uri
        {
            get { return _blob.Uri; }
        }

        public string Name
        {
            get { return _blob.Name; }
        }

        public DateTime LastModifiedUtc
        {
            get { return _blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue; }
        }

        public string ETag
        {
            get { return _blob.Properties.ETag; }
        }

        public Task DeleteIfExistsAsync()
        {
            return Task.Factory.FromAsync<bool>(
                    (cb, state) => _blob.BeginDeleteIfExists(deleteSnapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                    accessCondition: null,
                    options: null,
                    operationContext: null,
                    callback: cb,
                    state: state),
                    ar => _blob.EndDeleteIfExists(ar),
                    state: null);
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return DownloadToStreamAsync(target, accessCondition: null);
        }

        public Task DownloadToStreamAsync(Stream target, AccessCondition accessCondition)
        {
            // Note: Overloads of FromAsync that take an AsyncCallback and State to pass through are more efficient:
            //  http://blogs.msdn.com/b/pfxteam/archive/2009/06/09/9716439.aspx
            var options = new BlobRequestOptions()
            {
                // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
                RetryPolicy = new DontRetryOnNotModifiedPolicy(new LinearRetry())
            };

            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginDownloadToStream(
                    target,
                    accessCondition,
                    options: options,
                    operationContext: null,
                    callback: cb,
                    state: state),
                ar => _blob.EndDownloadToStream(ar),
                state: null);
        }

        public Task<bool> ExistsAsync()
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginExists(cb, state), 
                ar => _blob.EndExists(ar),
                state: null);
        }

        public Task SetPropertiesAsync()
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginSetProperties(cb, state), 
                ar => _blob.EndSetProperties(ar),
                state: null);
        }

        public async Task UploadFromStreamAsync(Stream packageFile, bool overwrite)
        {
            if (overwrite)
            {
                await _blob.UploadFromStreamAsync(packageFile);
            }
            else
            {
                await _blob.UploadFromStreamAsync(
                    packageFile,
                    AccessCondition.GenerateIfNoneMatchCondition("*"),
                    new BlobRequestOptions(),
                    new OperationContext());
            }
        }

        public Task FetchAttributesAsync()
        {
            return Task.Factory.FromAsync(
                (cb, state) => _blob.BeginFetchAttributes(cb, state),
                ar => _blob.EndFetchAttributes(ar),
                state: null);
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
