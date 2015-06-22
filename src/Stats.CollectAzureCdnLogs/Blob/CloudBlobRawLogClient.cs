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

        public async Task<bool> UploadBlobAsync(CloudBlobContainer targetContainer, RawLogFileInfo logFile, Stream rawLogStream)
        {
            if (targetContainer == null)
            {
                throw new ArgumentNullException("targetContainer");
            }
            if (logFile == null)
            {
                throw new ArgumentNullException("logFile");
            }
            if (rawLogStream == null)
            {
                throw new ArgumentNullException("rawLogStream");
            }

            var blobName = logFile.Uri.ToString();
            try
            {
                _jobEventSource.BeginningBlobUpload(blobName);

                // ensure we upload from the start of the stream
                rawLogStream.Position = 0;

                // ensure the .download suffix is trimmed away
                var fileName = logFile.FileName.Replace(".download", string.Empty);

                Trace.TraceInformation("Uploading file '{0}'.", fileName);

                var blob = targetContainer.GetBlockBlobReference(fileName);
                blob.Properties.ContentType = logFile.ContentType;

                // 3. Upload the file using the original file name.
                await blob.UploadFromStreamAsync(rawLogStream);

                Trace.TraceInformation("Finished uploading file '{0}' to '{1}'.", fileName, blob.Uri.AbsoluteUri);
                _jobEventSource.FinishingBlobUpload(blobName);
                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                _jobEventSource.FailedToUploadFile(blobName, exception.ToString());
            }
            return false;
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