// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace NuGetGallery
{
    public class CloudBlobWrapper : ISimpleCloudBlob
    {
        private const string ContentDispositionHeaderName = "Content-Disposition";
        private const string ContentEncodingHeaderName = "Content-Encoding";
        private const string ContentLanguageHeaderName = "Content-Language";
        private const string CacheControlHeaderName = "Cache-Control";
        private const string ContentMd5HeaderName = "Content-Md5";
        private readonly BlockBlobClient _blob;
        private readonly CloudBlobContainerWrapper _container;
        private string _lastSeenEtag = null;

        public ICloudBlobProperties Properties { get; private set; }
        public IDictionary<string, string> Metadata { get; private set; }
        public ICloudBlobCopyState CopyState { get; private set; }
        public Uri Uri
        {
            get
            {
                var builder = new UriBuilder(_blob.Uri);
                builder.Query = string.Empty;
                return builder.Uri;
            }
        }
        public string Name => _blob.Name;
        public string Container => _blob.BlobContainerName;
        public DateTime LastModifiedUtc => BlobProperties.LastModifiedUtc;
        public string ETag => _lastSeenEtag;
        public bool IsSnapshot => BlobProperties.IsSnapshot;

        internal Uri BlobSasUri { get; } = null;
        internal CloudBlobReadOnlyProperties BlobProperties { get; set; } = null;
        internal BlobHttpHeaders BlobHeaders { get; set; } = null;

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
                BlobProperties = new CloudBlobReadOnlyProperties(blobData);
                if (blobData.Properties != null)
                {
                    BlobHeaders = new BlobHttpHeaders();
                    BlobHeaders.ContentType = blobData.Properties.ContentType;
                    BlobHeaders.ContentDisposition = blobData.Properties.ContentDisposition;
                    BlobHeaders.ContentEncoding = blobData.Properties.ContentEncoding;
                    BlobHeaders.ContentLanguage = blobData.Properties.ContentLanguage;
                    BlobHeaders.CacheControl = blobData.Properties.CacheControl;
                    BlobHeaders.ContentHash = blobData.Properties.ContentHash;
                }
            }
        }

        internal CloudBlobWrapper(BlockBlobClient blob, Uri blobSasUri)
            : this(blob, container: null)
        {
            BlobSasUri = blobSasUri ?? throw new ArgumentNullException(nameof(blobSasUri));
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
            return new CloudBlobWrapper(blob, container: null);
        }

        public async Task<Stream> OpenReadAsync(IAccessCondition accessCondition)
        {
            BlobDownloadOptions options = null;
            if (accessCondition != null)
            {
                options = new BlobDownloadOptions()
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                };
            }
            var result = await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.DownloadStreamingAsync(options));
            if (result.GetRawResponse().Status == (int)HttpStatusCode.NotModified)
            {
                // calling code expects an exception thrown on not modified response
                throw new CloudBlobNotModifiedException(null);
            }
            UpdateEtag(result.Value.Details);
            return result.Value.Content;
        }

        public async Task<Stream> OpenWriteAsync(IAccessCondition accessCondition, string contentType = null)
        {
            BlockBlobOpenWriteOptions options = new BlockBlobOpenWriteOptions
            {
                OpenConditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
            };
            if (contentType != null)
            {
                options.HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                };
            }
            return await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                // overwrite must be set to true for BlockBlobClient.OpenWriteAsync call *shrug*
                // The value itself does not seem to be otherwise used anywhere.
                // https://github.com/Azure/azure-sdk-for-net/blob/aec1a1389636a2ef76270ab4bdcb0715a2abb1aa/sdk/storage/Azure.Storage.Blobs/src/BlockBlobClient.cs#L2776-L2779
                // https://github.com/Azure/azure-sdk-for-net/blob/aec1a1389636a2ef76270ab4bdcb0715a2abb1aa/sdk/storage/Azure.Storage.Blobs/tests/BlobClientOpenWriteTests.cs#L124-L133
                _blob.OpenWriteAsync(overwrite: true, options));
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
            // 304s are not retried with Azure.Storage.Blobs, so no need for custom retry policy.

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
                _blob.SetHttpHeadersAsync(BlobHeaders)));
        }

        public async Task SetPropertiesAsync(IAccessCondition accessCondition)
        {
            UpdateEtag(await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.SetHttpHeadersAsync(
                    BlobHeaders,
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
                BlobUploadOptions options = null;
                if (BlobHeaders != null)
                {
                    options = new BlobUploadOptions
                    {
                        HttpHeaders = BlobHeaders,
                    };
                }
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
            if (accessCondition != null || BlobHeaders != null)
            {
                options = new BlobUploadOptions
                {
                    Conditions = CloudWrapperHelpers.GetSdkAccessCondition(accessCondition),
                    HttpHeaders = BlobHeaders,
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
            BlobProperties = new CloudBlobReadOnlyProperties(blobProperties);
            ReplaceHttpHeaders(blobProperties);
            ReplaceMetadata(blobProperties.Metadata);
        }

        private void ReplaceHttpHeaders(BlobProperties blobProperties)
        {
            if (BlobHeaders == null)
            {
                BlobHeaders = new BlobHttpHeaders();
            }
            BlobHeaders.ContentType = blobProperties.ContentType;
            BlobHeaders.ContentDisposition = blobProperties.ContentDisposition;
            BlobHeaders.ContentEncoding = blobProperties.ContentEncoding;
            BlobHeaders.ContentLanguage = blobProperties.ContentLanguage;
            BlobHeaders.CacheControl = blobProperties.CacheControl;
            BlobHeaders.ContentHash = blobProperties.ContentHash;
        }

        private void ReplaceHttpHeaders(BlobDownloadDetails details)
        {
            if (BlobHeaders == null)
            {
                BlobHeaders = new BlobHttpHeaders();
            }
            BlobHeaders.ContentType = details.ContentType;
            BlobHeaders.ContentDisposition = details.ContentDisposition;
            BlobHeaders.ContentEncoding = details.ContentEncoding;
            BlobHeaders.ContentLanguage = details.ContentLanguage;
            BlobHeaders.CacheControl = details.CacheControl;
            BlobHeaders.ContentHash = details.ContentHash;
        }

        private void ReplaceHttpHeaders(ResponseHeaders headers)
        {
            if (BlobHeaders == null)
            {
                BlobHeaders = new BlobHttpHeaders();
            }
            BlobHeaders.ContentType = headers.ContentType;
            BlobHeaders.ContentDisposition = headers.TryGetValue(ContentDispositionHeaderName, out var contentDisposition) ? contentDisposition : null;
            BlobHeaders.ContentEncoding = headers.TryGetValue(ContentEncodingHeaderName, out var contentEncoding) ? contentEncoding : null;
            BlobHeaders.ContentLanguage = headers.TryGetValue(ContentLanguageHeaderName, out var contentLanguage) ? contentLanguage : null;
            BlobHeaders.CacheControl = headers.TryGetValue(CacheControlHeaderName, out var cacheControl) ? cacheControl : null;
            if (headers.TryGetValue(ContentMd5HeaderName, out var contentHash))
            {
                try
                {
                    BlobHeaders.ContentHash = Convert.FromBase64String(contentHash);
                }
                catch
                {
                    BlobHeaders.ContentHash = null;
                }
            }
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

            // We sort of have 4 cases here:
            // 1. sourceWrapper was created using connection string containing account key (shouldn't be the case any longer)
            //    In this case sourceWrapper._blob.Uri would be a "naked" URI to the blob request to which will fail unless blob is in
            //    the public container. However, in this case we'd be able to generate SAS URL to use to access it.
            // 2. sourceWrapper was created using connection string using SAS token. In this case sourceWrapper._blob.Uri will have
            //    the same SAS token attached to it automagically (that seems to be Azure.Storage.Blobs feature).
            // 3. sourceWrapper uses token credential (MSI or something else provided by Azure.Identity). In this case URI will still
            //    be naked blob URI. However, assuming destination blob also uses token credential, the underlying implementation
            //    (in Azure.Storage.Blobs) seem to use destination's token to try to access source and if that gives access,
            //    everything works. As long as we use the same credential to access both storage accounts (which should be true
            //    for all our services), it should also work.
            // 4. sourceWrapper has BlobSasUri property set (which is indicative of using ICloudBlobClient.GetBlobFromUri with SAS token
            //    to create the source object). The internal client has the SAS token properly set, but there is no way to fish it out
            //    so, we assume that property instead contains the appropriate URL that would allow copying from.
            //
            // If source blob is public none of the above matters.
            var sourceUri = sourceWrapper._blob.Uri;
            if (sourceWrapper._blob.CanGenerateSasUri)
            {
                sourceUri = sourceWrapper._blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(60));
            }
            else if (sourceWrapper.BlobSasUri != null)
            {
                sourceUri = sourceWrapper.BlobSasUri;
            }

            var options = new BlobCopyFromUriOptions
            {
                SourceConditions = CloudWrapperHelpers.GetSdkAccessCondition(sourceAccessCondition),
                DestinationConditions = CloudWrapperHelpers.GetSdkAccessCondition(destAccessCondition),
            };

            var copyOperation = await CloudWrapperHelpers.WrapStorageExceptionAsync(() =>
                _blob.StartCopyFromUriAsync(
                    sourceUri, options));
            await FetchAttributesAsync();
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
            if (response?.Headers != null)
            {
                if (response.Headers.ETag.HasValue)
                {
                    _lastSeenEtag = EtagToString(response.Headers.ETag.Value);
                }

                ReplaceHttpHeaders(response.Headers);
            }
            return response;
        }

        private Response<BlobProperties> UpdateEtag(Response<BlobProperties> propertiesResponse)
        {
            if (propertiesResponse?.Value != null)
            {
                _lastSeenEtag = EtagToString(propertiesResponse.Value.ETag);
            }
            return propertiesResponse;
        }
        
        private Response<BlobContentInfo> UpdateEtag(Response<BlobContentInfo> infoResponse)
        {
            if (infoResponse?.Value != null)
            {
                _lastSeenEtag = EtagToString(infoResponse.Value.ETag);
            }
            return infoResponse;
        }

        private Response<BlobInfo> UpdateEtag(Response<BlobInfo> infoResponse)
        {
            if (infoResponse?.Value != null)
            {
                _lastSeenEtag = EtagToString(infoResponse.Value.ETag);
            }
            return infoResponse;
        }

        private void UpdateEtag(BlobDownloadDetails details)
        {
            if (details != null)
            {
                _lastSeenEtag = EtagToString(details.ETag);
                ReplaceHttpHeaders(details);
                ReplaceMetadata(details.Metadata);
            }
        }

        // workaround for https://github.com/Azure/azure-sdk-for-net/issues/29942 
        private static string EtagToString(ETag etag)
            => etag.ToString("H");
    }
}