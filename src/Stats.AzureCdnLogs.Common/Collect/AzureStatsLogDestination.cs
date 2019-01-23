
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using ICSharpCode.SharpZipLib.GZip;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A <see cref="ILogDestination"/> implementation using Azure Storage as the storage. 
    /// </summary>
    public class AzureStatsLogDestination : ILogDestination
    {
        private const string ContentTypeGzip = "application/x-gzip";
        private const string ContentTypeText = "text/plain";

        private CloudStorageAccount _azureAccount;
        private CloudBlobClient _cloudBlobClient;
        private CloudBlobContainer _cloudBlobContainer;
        private readonly ILogger<AzureStatsLogDestination> _logger;

        public AzureStatsLogDestination(CloudStorageAccount storageAccount, string containerName, ILogger<AzureStatsLogDestination> logger)
        {
            _azureAccount = storageAccount;
            _cloudBlobClient = _azureAccount.CreateCloudBlobClient();
            _cloudBlobContainer = _cloudBlobClient.GetContainerReference(containerName);
            _cloudBlobContainer.CreateIfNotExists();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        /// <returns>Returns true if the action to write the destination file was successful. If the destination file was already present, the operation will return false without any action taken.</returns>
        public async Task<bool> WriteAsync(Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token)
        {
            _logger.LogInformation("WriteAsync: Start to write to {DestinationFileName}. ContentType is {ContentType}.", 
                $"{_cloudBlobContainer.StorageUri}{_cloudBlobContainer.Name}{destinationFileName}",
                destinationContentType);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("WriteAsync: The operation was cancelled.");
                return false;
            }
            var blob = _cloudBlobContainer.GetBlockBlobReference(destinationFileName);
            blob.Properties.ContentType = GetContentType(destinationContentType);

            // If the blob was written already to the destination do not do anything.
            // This should not happen if the renew task was correctly scheduled. Add the check just in case that the renew task was not scheduled in time and a different process already processed the file.
            if (!(await blob.ExistsAsync()))
            {
                var resultStream = await blob.OpenWriteAsync();
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
                if (!(await blob.ExistsAsync()))
                {
                    resultStream.Commit();
                    _logger.LogInformation("WriteAsync: End write to {DestinationFileName}", destinationFileName);
                    return true;
                }
            }
            _logger.LogInformation("WriteAsync: The destination file {DestinationFileName}, was already present.", destinationFileName);
            return false;
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
