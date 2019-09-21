// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.DataMovement;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Protocol;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage, IAzureStorage
    {
        private readonly bool _compressContent;
        private readonly IThrottle _throttle;
        private readonly ICloudBlobDirectory _directory;
        private readonly bool _useServerSideCopy;

        public const string Sha512HashAlgorithmId = "SHA512";
        public static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(10);

        public AzureStorage(
            CloudStorageAccount account,
            string containerName,
            string path,
            Uri baseAddress,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool useServerSideCopy,
            bool compressContent,
            bool verbose,
            bool initializeContainer,
            IThrottle throttle) : this(
                new CloudBlobDirectoryWrapper(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path)),
                baseAddress,
                maxExecutionTime,
                serverTimeout,
                initializeContainer)
        {
            _useServerSideCopy = useServerSideCopy;
            _compressContent = compressContent;
            _throttle = throttle ?? NullThrottle.Instance;
            Verbose = verbose;
        }

        public AzureStorage(
            ICloudBlobDirectory directory,
            Uri baseAddress,
            TimeSpan maxExecutionTime,
            TimeSpan serverTimeout,
            bool initializeContainer) : base(
                baseAddress ?? GetDirectoryUri(directory))
        {
            _directory = directory;

            // Unless overridden at the level of a single API call, these options will apply to all service calls that 
            // use BlobRequestOptions.
            _directory.ServiceClient.DefaultRequestOptions = new BlobRequestOptions()
            {
                ServerTimeout = serverTimeout,
                MaximumExecutionTime = maxExecutionTime,
                RetryPolicy = new ExponentialRetry()
            };

            if (initializeContainer)
            {
                if (_directory.Container.CreateIfNotExists())
                {
                    _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                    if (Verbose)
                    {
                        Trace.WriteLine(string.Format("Created '{0}' public container", _directory.Container.Name));
                    }
                }
            }
        }

        public override async Task<OptimisticConcurrencyControlToken> GetOptimisticConcurrencyControlTokenAsync(
            Uri resourceUri,
            CancellationToken cancellationToken)
        {
            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string blobName = GetName(resourceUri);
            CloudBlockBlob blob = GetBlockBlobReference(blobName);

            await blob.FetchAttributesAsync(cancellationToken);

            return new OptimisticConcurrencyControlToken(blob.Properties.ETag);
        }

        private static Uri GetDirectoryUri(ICloudBlobDirectory directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        //Blob exists
        public override bool Exists(string fileName)
        {
            Uri packageRegistrationUri = ResolveUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            CloudBlockBlob blob = GetBlockBlobReference(blobName);

            if (blob.Exists())
            {
                return true;
            }
            if (Verbose)
            {
                Trace.WriteLine(string.Format("The blob {0} does not exist.", packageRegistrationUri));
            }
            return false;
        }

        public override async Task<IEnumerable<StorageListItem>> ListAsync(CancellationToken cancellationToken)
        {
            var files = await _directory.ListBlobsAsync(cancellationToken);

            return files.Select(GetStorageListItem).AsEnumerable();
        }

        private StorageListItem GetStorageListItem(IListBlobItem listBlobItem)
        {
            var lastModified = (listBlobItem as CloudBlockBlob)?.Properties.LastModified?.UtcDateTime;

            return new StorageListItem(listBlobItem.Uri, lastModified);
        }

        protected override async Task OnCopyAsync(
            Uri sourceUri,
            IStorage destinationStorage,
            Uri destinationUri,
            IReadOnlyDictionary<string, string> destinationProperties,
            CancellationToken cancellationToken)
        {
            var azureDestinationStorage = destinationStorage as AzureStorage;

            if (azureDestinationStorage == null)
            {
                throw new NotImplementedException("Copying is only supported from Azure storage to Azure storage.");
            }

            string sourceName = GetName(sourceUri);
            string destinationName = azureDestinationStorage.GetName(destinationUri);

            CloudBlockBlob sourceBlob = GetBlockBlobReference(sourceName);
            CloudBlockBlob destinationBlob = azureDestinationStorage.GetBlockBlobReference(destinationName);

            var context = new SingleTransferContext();

            if (destinationProperties?.Count > 0)
            {
                context.SetAttributesCallback = new SetAttributesCallback((destination) =>
                {
                    var blob = (CloudBlockBlob)destination;

                    // The copy statement copied all properties from the source blob to the destination blob; however,
                    // there may be required properties on destination blob, all of which may have not already existed
                    // on the source blob at the time of copy.
                    foreach (var property in destinationProperties)
                    {
                        switch (property.Key)
                        {
                            case StorageConstants.CacheControl:
                                blob.Properties.CacheControl = property.Value;
                                break;

                            case StorageConstants.ContentType:
                                blob.Properties.ContentType = property.Value;
                                break;

                            default:
                                throw new NotImplementedException($"Storage property '{property.Value}' is not supported.");
                        }
                    }
                });
            }

            context.ShouldOverwriteCallback = new ShouldOverwriteCallback((source, destination) => true);

            await TransferManager.CopyAsync(sourceBlob, destinationBlob, _useServerSideCopy, options: null, context: context);
        }

        protected override async Task OnSaveAsync(Uri resourceUri, StorageContent content, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = GetBlockBlobReference(name);

            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = content.CacheControl;

            if (_compressContent)
            {
                blob.Properties.ContentEncoding = "gzip";
                using (Stream stream = content.GetContentStream())
                {
                    MemoryStream destinationStream = new MemoryStream();

                    using (GZipStream compressionStream = new GZipStream(destinationStream, CompressionMode.Compress, true))
                    {
                        await stream.CopyToAsync(compressionStream);
                    }

                    destinationStream.Seek(0, SeekOrigin.Begin);

                    var accessCondition = (content as StringStorageContentWithAccessCondition)?.AccessCondition;

                    await blob.UploadFromStreamAsync(
                        destinationStream, 
                        accessCondition, 
                        options: null, 
                        operationContext: null,
                        cancellationToken: cancellationToken);

                    Trace.WriteLine(string.Format("Saved compressed blob {0} to container {1}", blob.Uri.ToString(), _directory.Container.Name));
                }
            }
            else
            {
                using (Stream stream = content.GetContentStream())
                {
                    await blob.UploadFromStreamAsync(stream, cancellationToken);
                }

                Trace.WriteLine(string.Format("Saved uncompressed blob {0} to container {1}", blob.Uri.ToString(), _directory.Container.Name));
            }

            await TryTakeBlobSnapshotAsync(blob);
        }

        /// <summary>
        /// Take one snapshot only if there is not any snapshot for the specific blob
        /// This will prevent the blob to be deleted by a not intended delete action
        /// </summary>
        /// <param name="blob"></param>
        /// <returns></returns>
        private async Task<bool> TryTakeBlobSnapshotAsync(CloudBlockBlob blob)
        {
            if (blob == null)
            {
                //no action
                return false;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var allSnapshots = blob.Container.
                                   ListBlobs(prefix: blob.Name,
                                             useFlatBlobListing: true,
                                             blobListingDetails: BlobListingDetails.Snapshots);
                //the above call will return at least one blob the original
                if (allSnapshots.Count() == 1)
                {
                    var snapshot = await blob.CreateSnapshotAsync();
                    stopwatch.Stop();
                    Trace.WriteLine($"SnapshotCreated:milliseconds={stopwatch.ElapsedMilliseconds}:{blob.Uri.ToString()}:{snapshot.SnapshotQualifiedUri}");
                }
                return true;
            }
            catch (StorageException storageException)
            {
                stopwatch.Stop();
                Trace.WriteLine($"EXCEPTION:milliseconds={stopwatch.ElapsedMilliseconds}:CreateSnapshot: Failed to take the snapshot for blob {blob.Uri.ToString()}. Exception{storageException.ToString()}");
                return false;
            }
        }

        protected override async Task<StorageContent> OnLoadAsync(Uri resourceUri, CancellationToken cancellationToken)
        {
            // the Azure SDK will treat a starting / as an absolute URL,
            // while we may be working in a subdirectory of a storage container
            // trim the starting slash to treat it as a relative path
            string name = GetName(resourceUri).TrimStart('/');

            CloudBlockBlob blob = GetBlockBlobReference(name);

            await _throttle.WaitAsync();
            try
            {
                string content;

                using (var originalStream = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(originalStream, cancellationToken);

                    originalStream.Seek(0, SeekOrigin.Begin);

                    if (blob.Properties.ContentEncoding == "gzip")
                    {
                        using (var uncompressedStream = new GZipStream(originalStream, CompressionMode.Decompress))
                        {
                            using (var reader = new StreamReader(uncompressedStream))
                            {
                                content = await reader.ReadToEndAsync();
                            }
                        }
                    }
                    else
                    {
                        using (var reader = new StreamReader(originalStream))
                        {
                            content = await reader.ReadToEndAsync();
                        }
                    }
                }

                return new StringStorageContentWithETag(content, blob.Properties.ETag);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                if (Verbose)
                {
                    Trace.WriteLine(string.Format("Can't load '{0}'. Blob doesn't exist", resourceUri));
                }

                return null;
            }
            finally
            {
                _throttle.Release();
            }
        }

        protected override async Task OnDeleteAsync(Uri resourceUri, DeleteRequestOptions deleteRequestOptions, CancellationToken cancellationToken)
        {
            string name = GetName(resourceUri);

            var accessCondition = (deleteRequestOptions as DeleteRequestOptionsWithAccessCondition)?.AccessCondition;

            CloudBlockBlob blob = GetBlockBlobReference(name);
            await blob.DeleteAsync(deleteSnapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                                   accessCondition: accessCondition,
                                   options: null,
                                   operationContext: null,
                                   cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Returns the uri of the blob based on the Azure cloud directory
        /// </summary>
        /// <param name="name">The blob name.</param>
        /// <returns>The blob uri.</returns>
        public override Uri GetUri(string name)
        {
            var baseUri = _directory.Uri.AbsoluteUri;

            if (baseUri.EndsWith("/"))
            {
                return new Uri($"{baseUri}{name}", UriKind.Absolute);
            }

            return new Uri($"{baseUri}/{name}", UriKind.Absolute);
        }

        public override async Task<bool> AreSynchronized(Uri firstResourceUri, Uri secondResourceUri)
        {
            var source = new CloudBlockBlob(firstResourceUri);
            var destination = GetBlockBlobReference(GetName(secondResourceUri));

            // For interacting with the source, we just use the same blob request options as the destination blob.
            ApplyBlobRequestOptions(source);

            return await AreSynchronized(new AzureCloudBlockBlob(source), new AzureCloudBlockBlob(destination));
        }

        public async Task<bool> AreSynchronized(ICloudBlockBlob sourceBlockBlob, ICloudBlockBlob destinationBlockBlob)
        {
            if (await destinationBlockBlob.ExistsAsync(CancellationToken.None))
            {
                if (await sourceBlockBlob.ExistsAsync(CancellationToken.None))
                {
                    var sourceBlobMetadata = await sourceBlockBlob.GetMetadataAsync(CancellationToken.None);
                    var destinationBlobMetadata = await destinationBlockBlob.GetMetadataAsync(CancellationToken.None);
                    if (sourceBlobMetadata == null || destinationBlobMetadata == null)
                    {
                        return false;
                    }

                    var sourceBlobHasSha512Hash = sourceBlobMetadata.TryGetValue(Sha512HashAlgorithmId, out var sourceBlobSha512Hash);
                    var destinationBlobHasSha512Hash = destinationBlobMetadata.TryGetValue(Sha512HashAlgorithmId, out var destinationBlobSha512Hash);
                    if (!sourceBlobHasSha512Hash)
                    {
                        Trace.TraceWarning(string.Format("The source blob ({0}) doesn't have the SHA512 hash.", sourceBlockBlob.Uri.ToString()));
                    }
                    if (!destinationBlobHasSha512Hash)
                    {
                        Trace.TraceWarning(string.Format("The destination blob ({0}) doesn't have the SHA512 hash.", destinationBlockBlob.Uri.ToString()));
                    }
                    if (sourceBlobHasSha512Hash && destinationBlobHasSha512Hash)
                    {
                        if (sourceBlobSha512Hash == destinationBlobSha512Hash)
                        {
                            Trace.WriteLine(string.Format("The source blob ({0}) and destination blob ({1}) have the same SHA512 hash and are synchronized.",
                                sourceBlockBlob.Uri.ToString(), destinationBlockBlob.Uri.ToString()));
                            return true;
                        }

                        // The SHA512 hash between the source and destination blob should be always same.
                        Trace.TraceWarning(string.Format("The source blob ({0}) and destination blob ({1}) have the different SHA512 hash and are not synchronized. " +
                            "The source blob hash is {2} while the destination blob hash is {3}",
                            sourceBlockBlob.Uri.ToString(), destinationBlockBlob.Uri.ToString(), sourceBlobSha512Hash, destinationBlobSha512Hash));
                    }

                    return false;
                }
                return true;
            }
            return !(await sourceBlockBlob.ExistsAsync(CancellationToken.None));
        }

        public async Task<ICloudBlockBlob> GetCloudBlockBlobReferenceAsync(Uri blobUri)
        {
            string blobName = GetName(blobUri);
            CloudBlockBlob blob = GetBlockBlobReference(blobName);
            var blobExists = await blob.ExistsAsync();

            if (Verbose && !blobExists)
            {
                Trace.WriteLine($"The blob {blobUri.AbsoluteUri} does not exist.");
            }

            return new AzureCloudBlockBlob(blob);
        }

        public async Task<bool> HasPropertiesAsync(Uri blobUri, string contentType, string cacheControl)
        {
            var blobName = GetName(blobUri);
            var blob = GetBlockBlobReference(blobName);

            if (await blob.ExistsAsync())
            {
                await blob.FetchAttributesAsync();

                return string.Equals(blob.Properties.ContentType, contentType)
                    && string.Equals(blob.Properties.CacheControl, cacheControl);
            }

            return false;
        }

        private CloudBlockBlob GetBlockBlobReference(string blobName)
        {
            var blob = _directory.GetBlockBlobReference(blobName);

            ApplyBlobRequestOptions(blob);

            return blob;
        }

        private void ApplyBlobRequestOptions(CloudBlockBlob blob)
        {
            blob.ServiceClient.DefaultRequestOptions = _directory.ServiceClient.DefaultRequestOptions;
        }
    }
}