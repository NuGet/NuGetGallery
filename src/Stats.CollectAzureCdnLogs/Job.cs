// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureCdnLogs.Blob;
using Stats.CollectAzureCdnLogs.Ftp;

namespace Stats.CollectAzureCdnLogs
{
    public class Job
         : JobBase
    {
        private static readonly DateTime UnixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
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
                var ftpLogFolder = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUri);
                var azureCdnPlatform = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnPlatform);
                var cloudStorageAccount = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageAccount);
                _cloudStorageContainerName = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnCloudStorageContainerName);
                _azureCdnAccountNumber = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.AzureCdnAccountNumber);
                _ftpUsername = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourceUsername);
                _ftpPassword = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.FtpSourcePassword);

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

        public override async Task<bool> Run()
        {
            try
            {
                var ftpClient = new FtpRawLogClient(JobEventSource.Log, _ftpUsername, _ftpPassword);
                var azureClient = new CloudBlobRawLogClient(JobEventSource.Log, _cloudStorageAccount);

                // Collect directory listing.
                IEnumerable<RawLogFileInfo> rawLogFiles = await ftpClient.GetRawLogFiles(_ftpServerUri);

                // Prepare cloud storage blob container.
                var cloudBlobContainer = await azureClient.CreateContainerIfNotExistsAsync(_cloudStorageContainerName);

                foreach (var rawLogFile in rawLogFiles)
                {
                    try
                    {
                        // Only process the raw log files matching the target CDN platform and account number.
                        if (_azureCdnPlatform == rawLogFile.AzureCdnPlatform && _azureCdnAccountNumber.Equals(rawLogFile.AzureCdnAccountNumber, StringComparison.InvariantCultureIgnoreCase))
                        {
                            bool alreadyUploaded = false;
                            bool uploadSucceeded = false;
                            Uri rawLogUri = rawLogFile.Uri;

                            // Check if this is an already renamed file.
                            if (rawLogFile.IsPendingDownload)
                            {
                                // Check if the file has already been uploaded to blob storage.
                                alreadyUploaded = await azureClient.CheckIfBlobExistsAsync(cloudBlobContainer, rawLogFile);
                            }
                            else
                            {
                                // Rename the file on the origin to ensure we're not locking a file that still can be written to.
                                rawLogUri = await ftpClient.RenameAsync(rawLogFile, rawLogFile.FileName + FileExtensions.Download);

                                if (rawLogUri == null)
                                {
                                    // Failed to rename the file. Leave it and try again later.
                                    continue;
                                }
                            }

                            if (!alreadyUploaded)
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
                                        try
                                        {
                                            // ensure the .download suffix is trimmed away
                                            var fileName = rawLogFile.FileName.Replace(".download", string.Empty);

                                            Trace.TraceInformation("Uploading file '{0}'.", fileName);
                                            JobEventSource.Log.BeginningBlobUpload(rawLogFile.Uri.ToString());

                                            // open the resulting cleaned blob and stream modified entries
                                            // note the missing using() statement so that we can skip committing if an exception occurs
                                            var resultLogStream = await azureClient.OpenBlobForWriteAsync(cloudBlobContainer, rawLogFile, fileName);
                                            try
                                            {
                                                using (var resultGzipStream = new GZipOutputStream(resultLogStream))
                                                {
                                                    resultGzipStream.IsStreamOwner = false;

                                                    ProcessLogStream(rawGzipStream, resultGzipStream);

                                                    resultGzipStream.Flush();
                                                }

                                                // commit to blob storage
                                                resultLogStream.Commit();

                                                uploadSucceeded = true;
                                            }
                                            catch
                                            {
                                                resultLogStream = null;
                                                uploadSucceeded = false;
                                                throw;
                                            }

                                            Trace.TraceInformation("Finished uploading file '{0}' to '{1}'.", fileName, rawLogFile.Uri.AbsoluteUri);
                                            JobEventSource.Log.FinishingBlobUpload(rawLogFile.Uri.ToString());
                                        }
                                        catch (Exception exception)
                                        {
                                            Trace.TraceError(exception.ToString());
                                            JobEventSource.Log.FailedToUploadFile(rawLogFile.Uri.ToString(), exception.ToString());
                                        }
                                    }
                                }
                            }

                            // Delete the renamed file from the origin.
                            if (alreadyUploaded || uploadSucceeded)
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

        private static void ProcessLogStream(Stream sourceStream, Stream targetStream)
        {
            // note: not using async/await pattern as underlying streams do not support async
            using (var sourceStreamReader = new StreamReader(sourceStream))
            {
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    targetStreamWriter.Write("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");

                    do
                    {
                        var rawLogLine = sourceStreamReader.ReadLine();
                        var logLine = GetParsedModifiedLogEntry(rawLogLine);
                        if (!string.IsNullOrEmpty(logLine))
                        {
                            targetStreamWriter.Write(logLine);
                        }
                    } while (!sourceStreamReader.EndOfStream);
                }
            }
        }

        private static string GetParsedModifiedLogEntry(string rawLogEntry)
        {
            const string spaceCharacter = " ";
            const string dashCharacter = "-";
            var parsedEntry = CdnLogEntryParser.ParseLogEntryFromLine(rawLogEntry);

            if (parsedEntry == null)
            {
                return null;
            }

            var stringBuilder = new StringBuilder();

            // timestamp
            stringBuilder.Append(ToUnixTimeStamp(parsedEntry.EdgeServerTimeDelivered) + spaceCharacter);
            // time-taken
            stringBuilder.Append((parsedEntry.EdgeServerTimeTaken.HasValue ? parsedEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);

            // REMOVE c-ip
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // filesize
            stringBuilder.Append((parsedEntry.FileSize.HasValue ? parsedEntry.FileSize.Value.ToString() : dashCharacter) + spaceCharacter);
            // s-ip
            stringBuilder.Append((parsedEntry.EdgeServerIpAddress ?? dashCharacter) + spaceCharacter);
            // s-port
            stringBuilder.Append((parsedEntry.EdgeServerPort.HasValue ? parsedEntry.EdgeServerPort.Value.ToString() : dashCharacter) + spaceCharacter);
            // sc-status
            stringBuilder.Append((parsedEntry.CacheStatusCode ?? dashCharacter) + spaceCharacter);
            // sc-bytes
            stringBuilder.Append((parsedEntry.EdgeServerBytesSent.HasValue ? parsedEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // cs-method
            stringBuilder.Append((parsedEntry.HttpMethod ?? dashCharacter) + spaceCharacter);
            // cs-uri-stem
            stringBuilder.Append((parsedEntry.RequestUrl ?? dashCharacter) + spaceCharacter);

            // -
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // rs-duration
            stringBuilder.Append((parsedEntry.RemoteServerTimeTaken.HasValue ? parsedEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);
            // rs-bytes
            stringBuilder.Append((parsedEntry.RemoteServerBytesSent.HasValue ? parsedEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // c-referrer
            stringBuilder.Append((parsedEntry.Referrer ?? dashCharacter) + spaceCharacter);
            // c-user-agent
            stringBuilder.Append((parsedEntry.UserAgent ?? dashCharacter) + spaceCharacter);
            // customer-id
            stringBuilder.Append((parsedEntry.CustomerId ?? dashCharacter) + spaceCharacter);
            // x-ec_custom-1
            stringBuilder.AppendLine((parsedEntry.CustomField ?? dashCharacter) + spaceCharacter);

            return stringBuilder.ToString();
        }

        private static void TrySetLongProperty(Action<long?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(long.Parse(record));
            }
        }

        private static void TrySetIntProperty(Action<int?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(int.Parse(record));
            }
        }

        private static void TrySetStringProperty(Action<string> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(record);
            }
        }

        private static DateTime FromUnixTimestamp(string unixTimestamp)
        {
            // Unix timestamp is seconds past epoch
            var secondsPastEpoch = double.Parse(unixTimestamp);
            return UnixTimestamp + TimeSpan.FromSeconds(secondsPastEpoch);
        }

        private static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - UnixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }
    }
}
