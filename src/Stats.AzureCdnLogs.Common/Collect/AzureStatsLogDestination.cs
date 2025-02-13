// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using ICSharpCode.SharpZipLib.GZip;
using Azure.Storage.Blobs.Models;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A <see cref="ILogDestination"/> implementation using Azure Storage as the storage. 
    /// </summary>
    public class AzureStatsLogDestination : ILogDestination
    {
        private const string ContentTypeGzip = "application/x-gzip";
        private const string ContentTypeText = "text/plain";

        private BlobServiceClient _blobServiceClient;
        private BlobContainerClient _blobContainerClient;
        private readonly ILogger<AzureStatsLogDestination> _logger;

        public AzureStatsLogDestination(BlobServiceClient blobServiceClient, string containerName, ILogger<AzureStatsLogDestination> logger)
        {
            if (string.IsNullOrEmpty(containerName))
            {
                if (containerName == null)
                    throw new ArgumentNullException(nameof(containerName));
                else
                    throw new ArgumentException(nameof(containerName));
            }
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            _blobContainerClient.CreateIfNotExists();
        }

        /// <summary>
        /// Writes the input stream to the destination using the writeAction.
        /// If the destinationfile exists it will be overwritten.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="writeAction">The write action between the two streams.</param>
        /// <param name="destinationFileName">The destination file name.</param>
        /// <param name="destinationContentType">The destination content type.</param>
        /// <param name="token">A token to cancel the operation.</param>
        /// <returns>Returns true if the action to write the destination file was successful. If the destination file was already present, the operationResult will be false.
        /// If an Exception is thrown the exception will be stored under <see cref="AsyncOperationResult.OperationException"/>.
        /// </returns>

        public async Task<AsyncOperationResult> TryWriteAsync(Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token)
        {
            _logger.LogInformation("WriteAsync: Start to write to {DestinationFileName}. ContentType is {ContentType}.", 
                $"{_blobContainerClient.Uri}{destinationFileName}",
                destinationContentType);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("WriteAsync: The operation was cancelled. DestinationFileName {DestinationFileName}", destinationFileName);
                return new AsyncOperationResult(false, new OperationCanceledException(token));
            }
            try
            {
                var blobClient = _blobContainerClient.GetBlobClient(destinationFileName);
                var options = new BlobOpenWriteOptions()
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(destinationContentType) }
                };

                // If the blob was written already to the destination do not do anything.
                // This should not happen if the renew task was correctly scheduled. Add the check just in case that the renew task was not scheduled in time and a different process already processed the file.
                if (!(await blobClient.ExistsAsync(token)))
                {
                    // Do not use using to not automatically commit on dispose
                    // https://github.com/Azure/azure-storage-net/issues/832
                    var resultStream = await blobClient.OpenWriteAsync(overwrite: true, options, cancellationToken: token);
                    if (destinationContentType == ContentType.GZip)
                    {
                        using (var resultGzipStream = new GZipOutputStream(resultStream))
                        {
                            resultGzipStream.IsStreamOwner = false;
                            writeAction(inputStream, resultGzipStream);
                            await resultGzipStream.FlushAsync();
                        }
                    }
                    else
                    {
                        writeAction(inputStream, resultStream);
                    }

                    if(!(await blobClient.ExistsAsync(token)))
                    {
                        await resultStream.FlushAsync();
                        _logger.LogInformation("WriteAsync: End write to {DestinationFileName}", destinationFileName);
                        return new AsyncOperationResult(true, null);
                    }
                    
                }
                _logger.LogInformation("WriteAsync: The destination file {DestinationFileName}, was already present.", destinationFileName);
                return new AsyncOperationResult(false, null);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(LogEvents.FailedBlobUpload, exception, "WriteAsync: The destination file {DestinationFileName}, failed to be written.", destinationFileName);
                return new AsyncOperationResult(null, exception);
            }
        }

        private string GetContentType(ContentType contentType)
        {
            switch(contentType)
            {
                case ContentType.GZip:
                    return ContentTypeGzip;
                case ContentType.Text:
                    return ContentTypeText;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contentType));
            }
        }
    }
}
