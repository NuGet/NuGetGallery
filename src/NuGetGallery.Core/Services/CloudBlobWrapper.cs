// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private readonly BlockBlobClient _blob;
        private BlobProperties _blobProperties = null;

        public ICloudBlobProperties Properties { get; private set; }
        public IDictionary<string, string> Metadata => _blob.Metadata;
        public ICloudBlobCopyState CopyState { get; private set; }
        public Uri Uri => _blob.Uri;
        public string Name => _blob.Name;
        public DateTime LastModifiedUtc => _blob.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue;
        public string ETag => _blob.Properties.ETag;
        public bool IsSnapshot => _blob.IsSnapshot;

        public CloudBlobWrapper(BlockBlobClient blob)
        {
            _blob = blob;
            Properties = new CloudBlobPropertiesWrapper(_blob);
            CopyState = new CloudBlobCopyState(_blob);
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

            var blob = new BlockBlobClient(uri);
            return new CloudBlobWrapper(blob);
        }

        public async Task<Stream> OpenReadAsync(IAccessCondition accessCondition)
        {
            BlobOpenReadOptions options = null;
            if (accessCondition != null)
            {
                options = new BlobOpenReadOptions(allowModifications: false)
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.OpenReadAsync(options));
        }

        public async Task<Stream> OpenWriteAsync(IAccessCondition accessCondition)
        {
            BlockBlobOpenWriteOptions options = null;
            if (accessCondition != null)
            {
                options = new BlockBlobOpenWriteOptions
                {
                    OpenConditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                //TODO: how first argument interacts with access conditions?
                _blob.OpenWriteAsync(true, options));
        }

        public async Task DeleteIfExistsAsync()
        {
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots));
        }

        public Task DownloadToStreamAsync(Stream target)
        {
            return DownloadToStreamAsync(target, accessCondition: null);
        }

        public async Task DownloadToStreamAsync(Stream target, IAccessCondition accessCondition)
        {
            // Note: Overloads of FromAsync that take an AsyncCallback and State to pass through are more efficient:
            //  http://blogs.msdn.com/b/pfxteam/archive/2009/06/09/9716439.aspx
            var options = new BlobRequestOptions()
            {
                // The default retry policy treats a 304 as an error that requires a retry. We don't want that!
                RetryPolicy = new DontRetryOnNotModifiedPolicy(new LinearRetry())
            };

            BlobDownloadToOptions downloadOptions = null;
            if (accessCondition != null)
            {
                downloadOptions = new BlobDownloadToOptions
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.DownloadToAsync(target, options));
        }

        public async Task<bool> ExistsAsync()
        {
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.ExistsAsync());
        }

        public async Task SnapshotAsync(CancellationToken token)
        {
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.CreateSnapshotAsync(cancellationToken: token));
        }

        public async Task SetPropertiesAsync()
        {
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetPropertiesAsync());
        }

        public async Task SetPropertiesAsync(IAccessCondition accessCondition)
        {
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetPropertiesAsync(
                    CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                    options: null,
                    operationContext: null));
        }

        public async Task SetMetadataAsync(IAccessCondition accessCondition)
        {
            var props = (await _blob.GetPropertiesAsync()).Value;
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetMetadataAsync(
                    CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                    options: null,
                    operationContext: null));
        }

        public async Task UploadFromStreamAsync(Stream source, bool overwrite)
        {
            if (overwrite)
            {
                await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                    _blob.UploadAsync(source));
            }
            else
            {
                await UploadFromStreamAsync(source, AccessConditionWrapper.GenerateIfNoneMatchCondition("*"));
            }
        }

        public async Task UploadFromStreamAsync(Stream source, IAccessCondition accessCondition)
        {
            BlobUploadOptions options = null;
            if (accessCondition != null)
            {
                options = new BlobUploadOptions
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }
            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.UploadAsync(source, options));
        }

        public async Task FetchAttributesAsync()
        {
            _blobProperties = (await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.GetPropertiesAsync())).Value;
        }

        public string GetSharedAccessSignature(FileUriPermissions permissions, DateTimeOffset? endOfAccess)
        {
            var accessPolicy = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = endOfAccess,
                Permissions = CloudWrapperHelpers.GetSdkSharedAccessPermissions(permissions),
            };

            var signature = _blob.GetSharedAccessSignature(accessPolicy);

            return signature;
        }

        public async Task StartCopyAsync(ISimpleCloudBlob source, IAccessCondition sourceAccessCondition, IAccessCondition destAccessCondition)
        {
            // To avoid this we would need to somehow abstract away the primary and secondary storage locations. This
            // is not worth the effort right now!
            var sourceWrapper = source as CloudBlobWrapper;
            if (sourceWrapper == null)
            {
                throw new ArgumentException($"The source blob must be a {nameof(CloudBlobWrapper)}.");
            }

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.StartCopyAsync(
                    sourceWrapper._blob,
                    sourceAccessCondition: CloudWrapperHelpers.GetSdkAccessCondition(sourceAccessCondition),
                    destAccessCondition: CloudWrapperHelpers.GetSdkAccessCondition(destAccessCondition),
                    options: null,
                    operationContext: null));
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

            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.OpenReadAsync(accessCondition, blobRequestOptions, operationContext, cancellationToken));
        }

        public async Task<string> DownloadTextIfExistsAsync()
        {
            try
            {
                return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                    _blob.DownloadTextAsync());
            }
            catch (CloudBlobGenericNotFoundException)
            {
                return null;
            }
        }

        public async Task<bool> FetchAttributesIfExistsAsync()
        {
            try
            {
                await FetchAttributesAsync();
            }
            catch (CloudBlobGenericNotFoundException)
            {
                return false;
            }
            return true;
        }

        public async Task<Stream> OpenReadIfExistsAsync()
        {
            try
            {
                return await OpenReadAsync(accessCondition: null);
            }
            catch (CloudBlobGenericNotFoundException)
            {
                return null;
            }
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