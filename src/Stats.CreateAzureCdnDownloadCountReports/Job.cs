// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;

namespace Stats.CreateAzureCdnDownloadCountReports
{
    public class Job
        : JobBase
    {
        private const string _storedProcedureName = "[dbo].[SelectTotalDownloadCountsPerPackageVersion]";
        private const string _reportName = "downloads.v1.json";
        private CloudStorageAccount _cloudStorageAccount;
        private SqlConnectionStringBuilder _sourceDatabase;
        private string _destinationContainerName;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var cloudStorageAccountConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccountConnectionString);
                _sourceDatabase = new SqlConnectionStringBuilder(databaseConnectionString);
                _destinationContainerName = ValidateAzureContainerName(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName));

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }

        public override async Task<bool> Run()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // construct a cloud blob client for the configured storage account
                var cloudBlobClient = _cloudStorageAccount.CreateCloudBlobClient();
                cloudBlobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(10), 5);

                // Get the target blob container (to store the generated reports)
                var targetBlobContainer = cloudBlobClient.GetContainerReference(_destinationContainerName);
                await targetBlobContainer.CreateIfNotExistsAsync();
                var blobContainerPermissions = new BlobContainerPermissions();
                blobContainerPermissions.PublicAccess = BlobContainerPublicAccessType.Blob;
                await targetBlobContainer.SetPermissionsAsync(blobContainerPermissions);

                Trace.TraceInformation("Generating Download Count Report from {0}/{1} to {2}/{3}.", _sourceDatabase.DataSource, _sourceDatabase.InitialCatalog, _cloudStorageAccount.Credentials.AccountName, _destinationContainerName);

                // Gather download count data from statistics warehouse
                IReadOnlyCollection<DownloadCountData> downloadData;
                Trace.TraceInformation("Gathering Download Counts from {0}/{1}...", _sourceDatabase.DataSource, _sourceDatabase.InitialCatalog);
                using (var connection = await _sourceDatabase.ConnectTo())
                {
                    downloadData = (await connection.QueryWithRetryAsync<DownloadCountData>(_storedProcedureName, commandType: CommandType.StoredProcedure)).ToList();
                }
                Trace.TraceInformation("Gathered {0} rows of data.", downloadData.Count);

                // Group based on Package Id
                var grouped = downloadData.GroupBy(p => p.PackageId);
                var registrations = new JArray();
                foreach (var group in grouped)
                {
                    var details = new JArray();
                    details.Add(group.Key);
                    foreach (var gv in group)
                    {
                        var version = new JArray(gv.PackageVersion, gv.TotalDownloadCount);
                        details.Add(version);
                    }
                    registrations.Add(details);
                }

                var blob = targetBlobContainer.GetBlockBlobReference(_reportName);
                Trace.TraceInformation("Writing report to {0}", blob.Uri.AbsoluteUri);
                blob.Properties.ContentType = "application/json";
                await blob.UploadTextAsync(registrations.ToString(Formatting.None));
                Trace.TraceInformation("Wrote report to {0}", blob.Uri.AbsoluteUri);

                stopwatch.Stop();
                Trace.WriteLine("Time elapsed: " + stopwatch.Elapsed);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
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