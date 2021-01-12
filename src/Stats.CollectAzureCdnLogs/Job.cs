// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autofac;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureCdnLogs.Blob;
using Stats.CollectAzureCdnLogs.Ftp;

namespace Stats.CollectAzureCdnLogs
{
    public class Job : JsonConfigurationJob
    {
        private static readonly DateTime _unixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private Uri _ftpServerUri;
        private AzureCdnPlatform _azureCdnPlatform;
        private CloudStorageAccount _cloudStorageAccount;

        private CollectAzureCdnLogsConfiguration _configuration;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            InitializeJobConfiguration(_serviceProvider);
        }

        public void InitializeJobConfiguration(IServiceProvider serviceProvider)
        {
            _configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<CollectAzureCdnLogsConfiguration>>().Value;

            if (string.IsNullOrEmpty(_configuration.AzureCdnAccountNumber))
            {
                throw new ArgumentException("Configuration 'AzureCdnAccountNumber' is required", nameof(_configuration));
            }

            if (string.IsNullOrEmpty(_configuration.AzureCdnCloudStorageContainerName))
            {
                throw new ArgumentException("Configuration 'AzureCdnCloudStorageContainerName' is required", nameof(_configuration));
            }

            if (string.IsNullOrEmpty(_configuration.FtpSourceUsername)) {
                throw new ArgumentException("Configuration 'FtpSourceUsername' is required", nameof(_configuration));
            }

            if (string.IsNullOrEmpty(_configuration.FtpSourcePassword))
            {
                throw new ArgumentException("Configuration 'FtpSourcePassword' is required", nameof(_configuration));
            }

            _cloudStorageAccount = ValidateAzureCloudStorageAccount(_configuration.AzureCdnCloudStorageAccount);
            _azureCdnPlatform = ValidateAzureCdnPlatform(_configuration.AzureCdnPlatform);
            _ftpServerUri = ValidateFtpUri(_configuration.FtpSourceUri);
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
            var trimmedServerUrl = (serverUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedServerUrl))
            {
                throw new ArgumentException("FTP Server Uri is null or empty.", "serverUrl");
            }

            // if no protocol was specified assume ftp
            var schemeRegex = new Regex(@"^[a-zA-Z]+://");
            if (!schemeRegex.IsMatch(trimmedServerUrl))
            {
                trimmedServerUrl = string.Concat(@"ftp://", trimmedServerUrl);
            }

            var uri = new Uri(trimmedServerUrl);
            if (!uri.IsAbsoluteUri)
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must be an absolute URI. Value: '{0}'.", trimmedServerUrl));
            }

            // only ftp is supported but we could support others
            if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Uri must use the 'ftp://' scheme. Value: '{0}'.", trimmedServerUrl));
            }

            return uri;
        }

        public override async Task Run()
        {
            var ftpClient = new FtpRawLogClient(LoggerFactory, _configuration.FtpSourceUsername, _configuration.FtpSourcePassword);
            var azureClient = new CloudBlobRawLogClient(LoggerFactory, _cloudStorageAccount);

            // Collect directory listing.
            var rawLogFileUris = await ftpClient.GetRawLogFileUris(_ftpServerUri);

            // Prepare cloud storage blob container.
            var cloudBlobContainer = await azureClient.CreateContainerIfNotExistsAsync(_configuration.AzureCdnCloudStorageContainerName);

            foreach (var rawLogFileUri in rawLogFileUris)
            {
                try
                {
                    var rawLogFile = new RawLogFileInfo(rawLogFileUri);

                    if (_azureCdnPlatform != rawLogFile.AzureCdnPlatform
                        || !_configuration.AzureCdnAccountNumber.Equals(rawLogFile.AzureCdnAccountNumber, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Only process the raw log files matching the target CDN platform and account number.
                        continue;
                    }

                    var skipProcessing = false;
                    var uploadSucceeded = false;
                    var rawLogUri = rawLogFile.Uri;

                    // Check if this is an already renamed file:
                    // This would indicate that the file is being processed already (by another instance of this job),
                    // or that the file is being reprocessed (and the ".download" renamed file was left behind).
                    if (rawLogFile.IsPendingDownload)
                    {
                        // In order to support reprocessing ".gz" files,
                        // we only skip processing ".download" files that have been successfully uploaded to blob storage,
                        // which only happens when they have been processed successfully.
                        // Check if the original ".gz" file has already been uploaded to blob storage.
                        // If it already was uploaded to blob storage,
                        // we can skip processing this ".download" file and delete it from the FTP server.
                        var originalFileName = rawLogFile.FileName.Substring(0, rawLogFile.FileName.Length - FileExtensions.Download.Length);
                        skipProcessing = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, originalFileName);
                    }
                    else
                    {
                        // We are processing a ".gz" file.
                        // Check if the file has already been uploaded to blob storage: are we reprocessing it?
                        var isReprocessing = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, rawLogFile.FileName);

                        if (isReprocessing)
                        {
                            // As we are reprocessing this ".gz" file,
                            // we should first delete the ".download" file if it already exists on the FTP server.
                            var downloadFileUri = new Uri(rawLogFile.Uri + FileExtensions.Download);
                            await ftpClient.DeleteAsync(downloadFileUri);
                        }

                        // Rename the file on the origin to ensure we're not locking a file that still can be written to.
                        var downloadFileName = rawLogFile.FileName + FileExtensions.Download;
                        rawLogUri = await ftpClient.RenameAsync(rawLogFile, downloadFileName);

                        if (rawLogUri == null)
                        {
                            // Failed to rename the file. Leave it and try again later.
                            continue;
                        }
                    }

                    // Skip already processed ".download" files.
                    if (!skipProcessing)
                    {
                        // open the raw log from FTP
                        using (var rawLogStream = await ftpClient.OpenReadAsync(rawLogUri))
                        using (var rawLogStreamInMemory = new MemoryStream())
                        {
                            // copy the raw, compressed stream to memory - FTP does not like reading line by line
                            await rawLogStream.CopyToAsync(rawLogStreamInMemory);
                            rawLogStreamInMemory.Position = 0;

                            // process the raw, compressed memory stream
                            using (var rawGzipStream = new GZipInputStream(rawLogStreamInMemory))
                            {
                                // ensure the .download suffix is trimmed away
                                var fileName = rawLogFile.FileName.Replace(".download", string.Empty);

                                using (Logger.BeginScope("Started uploading file '{FileName}' to {BlobUri}.", fileName, rawLogFile.Uri.ToString()))
                                {
                                    try
                                    {
                                        // open the resulting cleaned blob and stream modified entries
                                        // note the missing using() statement so that we can skip committing if an exception occurs
                                        var resultLogStream = await azureClient.OpenBlobForWriteAsync(cloudBlobContainer, rawLogFile, fileName);

                                        try
                                        {
                                            using (var resultGzipStream = new GZipOutputStream(resultLogStream))
                                            {
                                                resultGzipStream.IsStreamOwner = false;

                                                ProcessLogStream(rawGzipStream, resultGzipStream, fileName);

                                                resultGzipStream.Flush();
                                            }

                                            // commit to blob storage
                                            resultLogStream.Commit();

                                            uploadSucceeded = true;
                                        }
                                        catch
                                        {
                                            uploadSucceeded = false;
                                            throw;
                                        }

                                        Logger.LogInformation("Finished uploading file.");
                                    }
                                    catch (Exception exception)
                                    {
                                        Logger.LogError(
                                            LogEvents.FailedBlobUpload,
                                            exception,
                                            LogMessages.FailedBlobUpload,
                                            rawLogUri);
                                    }
                                }
                            }
                        }
                    }

                    // Delete the renamed file from the origin.
                    if (skipProcessing || uploadSucceeded)
                    {
                        await ftpClient.DeleteAsync(rawLogUri);
                    }
                }
                catch (UnknownAzureCdnPlatformException exception)
                {
                    // Log the failing file, but ignore it. Other files should go through just fine.
                    Logger.LogWarning(
                        LogEvents.UnknownAzureCdnPlatform,
                        exception,
                        LogMessages.UnknownAzureCdnPlatform);
                }
                catch (InvalidRawLogFileNameException exception)
                {
                    // Log the failing file, but ignore it. Other files should go through just fine.
                    Logger.LogWarning(
                        LogEvents.InvalidRawLogFileName,
                        exception,
                        LogMessages.InvalidRawLogFileName);
                }
            }
        }

        private void ProcessLogStream(Stream sourceStream, Stream targetStream, string fileName)
        {
            // note: not using async/await pattern as underlying streams do not support async
            using (var sourceStreamReader = new StreamReader(sourceStream))
            {
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    targetStreamWriter.Write("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");

                    try
                    {
                        var lineNumber = 0;
                        do
                        {
                            var rawLogLine = sourceStreamReader.ReadLine();
                            lineNumber++;

                            var logLine = GetParsedModifiedLogEntry(lineNumber, rawLogLine, fileName);
                            if (!string.IsNullOrEmpty(logLine))
                            {
                                targetStreamWriter.Write(logLine);
                            }
                        }
                        while (!sourceStreamReader.EndOfStream);
                    }
                    catch (SharpZipBaseException e)
                    {
                        // this raw log file may be corrupt...
                        Logger.LogError(LogEvents.FailedToProcessLogStream, e, LogMessages.ProcessingLogStreamFailed);

                        throw;
                    }
                }
            }
        }

        private string GetParsedModifiedLogEntry(int lineNumber, string rawLogEntry, string fileName)
        {
            var parsedEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                lineNumber,
                rawLogEntry,
                (e, line) => Logger.LogError(
                    LogEvents.FailedToParseLogFileEntry,
                    e,
                    LogMessages.ParseLogEntryLineFailed,
                    fileName,
                    line));

            if (parsedEntry == null)
            {
                return null;
            }

            const string spaceCharacter = " ";
            const string dashCharacter = "-";

            var stringBuilder = new StringBuilder();

            // timestamp
            stringBuilder
                .Append(ToUnixTimeStamp(parsedEntry.EdgeServerTimeDelivered))
                .Append(spaceCharacter);
            // time-taken
            stringBuilder
                .Append((parsedEntry.EdgeServerTimeTaken.HasValue ? parsedEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);

            // REMOVE c-ip
            stringBuilder
                .Append(dashCharacter)
                .Append(spaceCharacter);

            // filesize
            stringBuilder
                .Append((parsedEntry.FileSize.HasValue ? parsedEntry.FileSize.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);
            // s-ip
            stringBuilder
                .Append((parsedEntry.EdgeServerIpAddress ?? dashCharacter))
                .Append(spaceCharacter);
            // s-port
            stringBuilder
                .Append((parsedEntry.EdgeServerPort.HasValue ? parsedEntry.EdgeServerPort.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);
            // sc-status
            stringBuilder
                .Append((parsedEntry.CacheStatusCode ?? dashCharacter))
                .Append(spaceCharacter);
            // sc-bytes
            stringBuilder
                .Append((parsedEntry.EdgeServerBytesSent.HasValue ? parsedEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);
            // cs-method
            stringBuilder
                .Append((parsedEntry.HttpMethod ?? dashCharacter))
                .Append(spaceCharacter);
            // cs-uri-stem
            stringBuilder
                .Append((parsedEntry.RequestUrl ?? dashCharacter))
                .Append(spaceCharacter);

            // -
            stringBuilder
                .Append(dashCharacter)
                .Append(spaceCharacter);

            // rs-duration
            stringBuilder
                .Append((parsedEntry.RemoteServerTimeTaken.HasValue ? parsedEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);
            // rs-bytes
            stringBuilder
                .Append((parsedEntry.RemoteServerBytesSent.HasValue ? parsedEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter))
                .Append(spaceCharacter);
            // c-referrer
            stringBuilder
                .Append((parsedEntry.Referrer ?? dashCharacter))
                .Append(spaceCharacter);
            // c-user-agent
            stringBuilder
                .Append((parsedEntry.UserAgent ?? dashCharacter))
                .Append(spaceCharacter);
            // customer-id
            stringBuilder
                .Append((parsedEntry.CustomerId ?? dashCharacter))
                .Append(spaceCharacter);
            // x-ec_custom-1
            stringBuilder
                .AppendLine((parsedEntry.CustomField ?? dashCharacter));

            return stringBuilder.ToString();
        }

        private static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - _unixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<CollectAzureCdnLogsConfiguration>(services, configurationRoot);
        }
    }
}
