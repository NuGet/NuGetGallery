// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Common;
using Stats.CollectAzureCdnLogs.Blob;
using Stats.CollectAzureCdnLogs.Ftp;

namespace Stats.CollectAzureCdnLogs
{
    public class Job
         : JobBase
    {
        private Uri _ftpServerUri;
        private string _ftpUsername;
        private string _ftpPassword;
        private string _azureCdnAccountNumber;
        private AzureCdnPlatform _azureCdnPlatform;
        private CloudStorageAccount _cloudStorageAccount;
        private string _cloudStorageContainerName;

        public Job()
            : base(JobEventSource.Log)
        {
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var ftpLogFolder = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUri);
                var azureCdnPlatform = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnPlatform);
                var cloudStorageAccount = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                _cloudStorageContainerName = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName);
                _azureCdnAccountNumber = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnAccountNumber);
                _ftpUsername = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUsername);
                _ftpPassword = JobConfigManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourcePassword);

                _ftpServerUri = ValidateFtpUri(ftpLogFolder);
                _azureCdnPlatform = ValidateAzureCdnPlatform(azureCdnPlatform);
                _cloudStorageAccount = ValidateAzureCloudStorageAccount(cloudStorageAccount);

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
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

        private static Uri ValidateFtpUri(string serverUrl)
        {
            string trimmedServerUrl = (serverUrl ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(trimmedServerUrl))
            {
                throw new ArgumentException("FTP Server Uri is null or empty.", "serverUrl");
            }

            // if no protocol was specified assume ftp
            Regex schemeRegex = new Regex(@"^[a-zA-Z]+://");
            if (!schemeRegex.IsMatch(trimmedServerUrl))
            {
                trimmedServerUrl = String.Concat(@"ftp://", trimmedServerUrl);
            }

            Uri uri = new Uri(trimmedServerUrl);

            if (!uri.IsAbsoluteUri)
            {
                throw new UriFormatException(String.Format(CultureInfo.CurrentCulture, "FTP Server Uri must be an absolute URI. Value: '{0}'.", trimmedServerUrl));
            }

            // only ftp is supported but we could support others
            if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                throw new UriFormatException(String.Format(CultureInfo.CurrentCulture, "FTP Server Uri must use the 'ftp://' scheme. Value: '{0}'.", trimmedServerUrl));
            }

            return uri;
        }

        public override async Task<bool> Run()
        {
            try
            {
                var ftpClient = new FtpRawLogClient(JobEventSource.Log, _ftpUsername, _ftpPassword);
                var azureClient = new CloudBlobRawLogClient(JobEventSource.Log, _cloudStorageAccount);

                // collect directory listing
                IEnumerable<RawLogFileInfo> rawLogFiles = await ftpClient.GetRawLogFiles(_ftpServerUri);

                // prepare cloud storage blob container
                var cloudBlobContainer = await azureClient.CreateContainerIfNotExistsAsync(_cloudStorageContainerName);

                foreach (var rawLogFile in rawLogFiles)
                {
                    try
                    {
                        // only process the raw log files matching the target CDN platform and account number
                        if (_azureCdnPlatform == rawLogFile.AzureCdnPlatform && _azureCdnAccountNumber.Equals(rawLogFile.AzureCdnAccountNumber, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // 1. Rename the file on the origin to ensure we're not locking a file that still can be written to.
                            Uri rawLogUri = await ftpClient.RenameAsync(rawLogFile, rawLogFile.FileName + FileExtensions.Download);

                            if (rawLogUri == null)
                            {
                                // Failed to rename the file. Leave it and try again later.
                                continue;
                            }

                            // 2. Stream the renamed file to blob storage.
                            bool uploadSucceeded;
                            using (var rawLogStream = await ftpClient.OpenReadAsync(rawLogUri))
                            {
                                uploadSucceeded = await azureClient.UploadBlobAsync(cloudBlobContainer, rawLogFile, rawLogStream);
                            }

                            // 4. Delete the renamed file from the origin.
                            if (uploadSucceeded)
                            {
                                await ftpClient.DeleteAsync(rawLogUri);
                            }
                        }
                    }
                    catch (UnknownAzureCdnPlatformException exception)
                    {
                        // Trace, but ignore the failing file. Other files should go through just fine.
                        Trace.TraceWarning(exception.ToString());
                    }
                    catch (InvalidRawLogFileNameException exception)
                    {
                        // Trace, but ignore the failing file. Other files should go through just fine.
                        Trace.TraceWarning(exception.ToString());
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }
    }
}
