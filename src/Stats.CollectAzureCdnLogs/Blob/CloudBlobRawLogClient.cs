// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace Stats.CollectAzureCdnLogs.Blob
{
    internal sealed class CloudBlobRawLogClient
    {
        private readonly JobEventSource _jobEventSource;
        private readonly CloudStorageAccount _cloudStorageAccount;

        public CloudBlobRawLogClient(JobEventSource jobEventSource, CloudStorageAccount cloudStorageAccount)
        {
            _jobEventSource = jobEventSource;
            _cloudStorageAccount = cloudStorageAccount;
        }

        public async Task<CloudBlobContainer> CreateContainerIfNotExistsAsync(string containerName)
        {
            var cloudBlobClient = _cloudStorageAccount.CreateCloudBlobClient();
            cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

            var cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            await cloudBlobContainer.CreateIfNotExistsAsync();

            return cloudBlobContainer;
        }

        public async Task<CloudBlobStream> OpenBlobForWriteAsync(CloudBlobContainer targetContainer, RawLogFileInfo logFile, string fileName)
        {
            if (targetContainer == null)
            {
                throw new ArgumentNullException("targetContainer");
            }
            if (logFile == null)
            {
                throw new ArgumentNullException("logFile");
            }

            var blobName = logFile.Uri.ToString();

            _jobEventSource.BeginningBlobUpload(blobName);

            var blob = targetContainer.GetBlockBlobReference(fileName);
            blob.Properties.ContentType = logFile.ContentType;

            // return a writeable stream
            return await blob.OpenWriteAsync();
        }

        public async Task<bool> CheckIfBlobExistsAsync(CloudBlobContainer targetContainer, RawLogFileInfo logFile)
        {
            try
            {
                Trace.TraceInformation("Checking if file '{0}' exists.", logFile.FileName);

                var blob = targetContainer.GetBlockBlobReference(logFile.FileName);
                bool exists = await blob.ExistsAsync();

                Trace.TraceInformation("Finished checking if file '{0}' exists (exists = {1}.", logFile.FileName, exists);
                return exists;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }
    }
}