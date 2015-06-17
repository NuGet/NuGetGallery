// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGet.Jobs.Common;
using Stats.AzureCdnLogs.Common;

namespace Stats.ParseAzureCdnLogs
{
    public class Job
        : JobBase
    {
        private string _azureCdnAccountNumber;
        private AzureCdnPlatform _azureCdnPlatform;
        private CloudStorageAccount _cloudStorageAccount;
        private string _cloudStorageContainerName;
        private string _cloudStorageTableName;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var azureCdnPlatform = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnPlatform);
                var cloudStorageAccount = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);

                _azureCdnAccountNumber = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnAccountNumber);
                _cloudStorageContainerName = ValidateAzureContainerName(JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName));
                _cloudStorageTableName = ValidateAzureTableName(JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageTableName));
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccount);
                _azureCdnPlatform = ValidateAzureCdnPlatform(azureCdnPlatform);

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            try
            {
                // construct a cloud blob client for the configured storage account
                var cloudBlobClient = _cloudStorageAccount.CreateCloudBlobClient();
                cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

                // Get the source blob container (containing compressed log files)
                // and construct a log source (fetching raw logs from the source blob container)
                var sourceBlobContainer = cloudBlobClient.GetContainerReference(_cloudStorageContainerName);
                var logSource = new CloudBlobRawLogSource(JobEventSource.Log, sourceBlobContainer);

                // Get the target table (for parsed raw data entries), a target blob container (for archiving decompressed log files) and create a parser
                var targetBlobContainer = cloudBlobClient.GetContainerReference(_cloudStorageContainerName + "-archive");
                await targetBlobContainer.CreateIfNotExistsAsync();

                var targetTable = new CdnLogEntryTable(_cloudStorageAccount, _cloudStorageTableName);
                await targetTable.CreateIfNotExists();

                // Get the dead-letter table (corrupted or failed blobs will end up there)
                var deadLetterBlobContainer = cloudBlobClient.GetContainerReference(_cloudStorageContainerName + "-deadletter");
                await deadLetterBlobContainer.CreateIfNotExistsAsync();

                var parser = new CloudTableLogParser(JobEventSource.Log, targetBlobContainer, targetTable, deadLetterBlobContainer);

                // Get the next to-be-processed raw log file using the cdn raw log file name prefix
                var prefix = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_", _azureCdnPlatform.GetRawLogFilePrefix(), _azureCdnAccountNumber);
                var logFiles = await logSource.ListNextLogFileToBeProcessedAsync(prefix);
                foreach (var logFile in logFiles)
                {
                    // Get the source blob
                    var logFileBlob = sourceBlobContainer.GetBlockBlobReference(logFile.Uri.Segments.Last());

                    await parser.ParseLogFileAsync(logFileBlob);
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }


        private static CloudStorageAccount ValidateAzureCloudStorageAccount(string cloudStorageAccount)
        {
            if (string.IsNullOrEmpty(cloudStorageAccount))
            {
                throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is not defined.");
            }

            CloudStorageAccount account;
            if (CloudStorageAccount.TryParse(cloudStorageAccount, out account))
            {
                return account;
            }
            throw new ArgumentException("Job parameter for Azure CDN Cloud Storage Account is invalid.");
        }

        private static AzureCdnPlatform ValidateAzureCdnPlatform(string azureCdnPlatform)
        {
            if (string.IsNullOrEmpty(azureCdnPlatform))
            {
                throw new ArgumentException("Job parameter for Azure CDN Platform is not defined.");
            }

            AzureCdnPlatform value;
            if (Enum.TryParse(azureCdnPlatform, true, out value))
            {
                return value;
            }
            throw new ArgumentException("Job parameter for Azure CDN Platform is invalid. Allowed values are: HttpLargeObject, HttpSmallObject, ApplicationDeliveryNetwork, FlashMediaStreaming.");
        }

        private static string ValidateAzureTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Table Name is not defined.");
            }
            if (!Regex.IsMatch(tableName, "^[A-Za-z][A-Za-z0-9]{2,62}$"))
            {
                throw new ArgumentException("Job parameter for Azure Storage Table Name is invalid.");
            }
            return tableName;
        }

        private static string ValidateAzureContainerName(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Job parameter for Azure Storage Container Name is not defined.");
            }
            return containerName;
        }
    }
}