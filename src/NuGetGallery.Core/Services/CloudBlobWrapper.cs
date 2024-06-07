// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private readonly BlockBlobClient _blob;
        private readonly CloudBlobContainerWrapper _container;
        internal CloudBlobReadOnlyProperties _blobProperties = null;
        internal BlobHttpHeaders _blobHeaders = null;
        private string _lastSeenEtag = null;

        public ICloudBlobProperties Properties { get; private set; }
        public IDictionary<string, string> Metadata { get; private set; }
        public ICloudBlobCopyState CopyState { get; private set; }
        public Uri Uri => _blob.Uri;
        public string Name => _blob.Name;
        public string Container => _blob.BlobContainerName;
        public DateTime LastModifiedUtc => _blobProperties.LastModifiedUtc;
        public string ETag => _lastSeenEtag;
        public bool IsSnapshot => _blobProperties.IsSnapshot;

        public CloudBlobWrapper(BlockBlobClient blob, CloudBlobContainerWrapper container)
        {
            _blob = blob ?? throw new ArgumentNullException(nameof(blob));
            _container = container; // container can be null

            Properties = new CloudBlobPropertiesWrapper(this);
            CopyState = new CloudBlobCopyState(this);
        }

        public CloudBlobWrapper(BlockBlobClient blob, BlobItem blobData, CloudBlobContainerWrapper container)
            : this(blob, container)
        {
            if (blobData != null)
            {
                ReplaceMetadata(blobData.Metadata);
                _blobProperties = new CloudBlobReadOnlyProperties(blobData);
                if (blobData.Properties != null)
                {
                    _blobHeaders = new BlobHttpHeaders();
                    _blobHeaders.ContentType = blobData.Properties.ContentType;
                    _blobHeaders.ContentDisposition = blobData.Properties.ContentDisposition;
                    _blobHeaders.ContentEncoding = blobData.Properties.ContentEncoding;
                    _blobHeaders.ContentLanguage = blobData.Properties.ContentLanguage;
                    _blobHeaders.CacheControl = blobData.Properties.CacheControl;
                    _blobHeaders.ContentHash = blobData.Properties.ContentHash;
                }
            }
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
            return new CloudBlobWrapper(blob, null);
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
            // 304s are not retried with Azure.Storage.Blobs, so need for custom retry policy

            BlobDownloadToOptions downloadOptions = null;
            if (accessCondition != null)
            {
                downloadOptions = new BlobDownloadToOptions
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }

            var response = UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.DownloadToAsync(target, downloadOptions)));

            if (response.Status == (int)HttpStatusCode.NotModified)
            {
                // calling code expects an exception thrown on not modified response
                throw new CloudBlobNotModifiedException(null);
            }
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
            UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetHttpHeadersAsync(_blobHeaders)));
        }

        public async Task SetPropertiesAsync(IAccessCondition accessCondition)
        {
            UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetHttpHeadersAsync(
                    _blobHeaders,
                    CloudWrapperHelpers.GetSdkAccessCondition(accessCondition))));
        }

        public async Task SetMetadataAsync(IAccessCondition accessCondition)
        {
            UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetMetadataAsync(
                    Metadata,
                    CloudWrapperHelpers.GetSdkAccessCondition(accessCondition))));
        }

        public async Task UploadFromStreamAsync(Stream source, bool overwrite)
        {
            if (overwrite)
            {
                UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                    _blob.UploadAsync(source)));
                await FetchAttributesAsync();
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
            UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.UploadAsync(source, options)));
            await FetchAttributesAsync();
        }

        public async Task FetchAttributesAsync()
        {
            var blobProperties = UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.GetPropertiesAsync())).Value;
            _blobProperties = new CloudBlobReadOnlyProperties(blobProperties);
            ReplaceHttpHeaders(blobProperties);
            ReplaceMetadata(blobProperties.Metadata);
        }

        private void ReplaceHttpHeaders(BlobProperties blobProperties)
        {
            if (_blobHeaders == null)
            {
                _blobHeaders = new BlobHttpHeaders();
            }
            _blobHeaders.ContentType = blobProperties.ContentType;
            _blobHeaders.ContentDisposition = blobProperties.ContentDisposition;
            _blobHeaders.ContentEncoding = blobProperties.ContentEncoding;
            _blobHeaders.ContentLanguage = blobProperties.ContentLanguage;
            _blobHeaders.CacheControl = blobProperties.CacheControl;
            _blobHeaders.ContentHash = blobProperties.ContentHash;
        }

        private void ReplaceHttpHeaders(BlobDownloadDetails details)
        {
            if (_blobHeaders == null)
            {
                _blobHeaders = new BlobHttpHeaders();
            }
            _blobHeaders.ContentType = details.ContentType;
            _blobHeaders.ContentDisposition = details.ContentDisposition;
            _blobHeaders.ContentEncoding = details.ContentEncoding;
            _blobHeaders.ContentLanguage = details.ContentLanguage;
            _blobHeaders.CacheControl = details.CacheControl;
            _blobHeaders.ContentHash = details.ContentHash;
        }

        private void ReplaceMetadata(IDictionary<string, string> newMetadata)
        {
            if (Metadata == null)
            {
                Metadata = new Dictionary<string, string>();
            }
            Metadata.Clear();
            if (newMetadata != null)
            {
                foreach (var kvp in newMetadata)
                {
                    Metadata.Add(kvp.Key, kvp.Value);
                }
            }
        }

        public async Task<string> GetSharedAccessSignature(FileUriPermissions permissions, DateTimeOffset endOfAccess)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _blob.BlobContainerName,
                BlobName = _blob.Name,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = endOfAccess,
            };
            sasBuilder.SetPermissions(CloudWrapperHelpers.GetSdkSharedAccessPermissions(permissions));

            if (_blob.CanGenerateSasUri)
            {
                // regular SAS
                return _blob.GenerateSasUri(sasBuilder).Query;
            }
            else if (_container?.Account?.UsingTokenCredential == true && _container?.Account?.Client != null)
            {
                // user delegation SAS
                var userDelegationKey = (await _container.Account.Client.GetUserDelegationKeyAsync(sasBuilder.StartsOn, sasBuilder.ExpiresOn)).Value;
                var blobUriBuilder = new BlobUriBuilder(_blob.Uri)
                {
                    Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, _blob.AccountName),
                };
                return blobUriBuilder.ToUri().Query;
            }
            else
            {
                throw new InvalidOperationException("Unsupported blob authentication");
            }
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

            // We sort of have 3 cases here:
            // 1. sourceWrapper was created using connections string containing account key (shouldn't be the case any longer)
            //    In this case sourcWrapper.Uri would be a "naked" URI to the blob request to which will fail unless blob is in
            //    the public container. However, in this case we'd be able to generate SAS URL to use to access it.
            // 2. sourceWrapper was created using connection string using SAS token. In this case sourceWrapper.Uri will have
            //    the same SAS token attached to it automagically (that seems to be Azure.Storage.Blobs feature).
            // 3. sourceWrapper uses token credential (MSI or something else provided by Azure.Identity). In this case URI will still
            //    be naked blob URI. However, assuming destination blob also uses token credential, the implementation seem to use
            //    destination's token to try to access source and if that gives access, everything works. As long as we use the same
            //    credential to access both storage accounts (which should be true for all our services), it should also work.
            //
            // If source blob is public none of the above matters.
            var sourceUri = sourceWrapper.Uri;
            if (sourceWrapper._blob.CanGenerateSasUri)
            {
                sourceUri = sourceWrapper._blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(60));
            }

            var options = new BlobCopyFromUriOptions
            {
                SourceConditions = CloudWrapperHelpers.GetSdkAccessCondition(sourceAccessCondition),
                DestinationConditions = CloudWrapperHelpers.GetSdkAccessCondition(destAccessCondition),
            };

            await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.StartCopyFromUriAsync(
                    sourceUri, options));
        }

        public async Task<Stream> OpenReadStreamAsync(
            TimeSpan serverTimeout,
            CancellationToken cancellationToken)
        {
            BlockBlobClient newClient;
            BlobClientOptions options = new BlobClientOptions
            {
                Retry = {
                    NetworkTimeout = serverTimeout,
                    Mode = Azure.Core.RetryMode.Exponential,
                },
            };
            if (_container?.Account != null)
            {
                newClient = _container.Account.CreateBlockBlobClient(this, options);
            }
            else
            {
                // this might happen if we created blob wrapper from URL, we'll assume authentication
                // is built into URI or blob is public.
                newClient = new BlockBlobClient(_blob.Uri, options);
            }
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                newClient.OpenReadAsync(options: null, cancellationToken));
        }

        public async Task<string> DownloadTextIfExistsAsync()
        {
            try
            {
                var content = await CloudWrapperHelpers.WrapStorageExceptionAsync(() => _blob.DownloadContentAsync());
                UpdateEtag(content.Value.Details);
                return content.Value.Content.ToString();
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

        private Response UpdateEtag(Response response)
        {
            if (response?.Headers.ETag != null)
            {
                _lastSeenEtag = response.Headers.ETag.ToString();
            }
            return response;
        }

        private Response<BlobProperties> UpdateEtag(Response<BlobProperties> propertiesResponse)
        {
            if (propertiesResponse?.Value != null)
            {
                _lastSeenEtag = propertiesResponse.Value.ETag.ToString();
            }
            return propertiesResponse;
        }
        
        private Response<BlobContentInfo> UpdateEtag(Response<BlobContentInfo> infoResponse)
        {
            if (infoResponse?.Value != null)
            {
                _lastSeenEtag = infoResponse.Value.ETag.ToString();
            }
            return infoResponse;
        }

        private Response<BlobInfo> UpdateEtag(Response<BlobInfo> infoResponse)
        {
            if (infoResponse?.Value != null)
            {
                _lastSeenEtag = infoResponse.Value.ETag.ToString();
            }
            return infoResponse;
        }

        private void UpdateEtag(BlobDownloadDetails details)
        {
            if (details != null)
            {
                _lastSeenEtag = details.ETag.ToString();
                ReplaceHttpHeaders(details);
            }
        }
    }
}