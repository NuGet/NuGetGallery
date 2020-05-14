// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// A representation of a Stats collector. 
    /// A collector is a type that copies the files from a <see cref="Stats.AzureCdnLogs.Common.Collect.ILogSource"/> to a <see cref="Stats.AzureCdnLogs.Common.Collect.ILogDestination"/>.
    /// The collector can also transform the lines from the source during the processing.
    /// </summary>
    public abstract class Collector
    {
        private static readonly DateTime _unixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        protected ILogSource _source;
        protected ILogDestination _destination;
        protected readonly ILogger<Collector> _logger;

        /// <summary>
        /// Used by UnitTests
        /// </summary>
        public Collector()
        { }

        /// <summary>
        /// .ctor for the Collector
        /// </summary>
        /// <param name="source">The source of the Collector.</param>
        /// <param name="destination">The destination for the collector.</param>
        /// <param name="logger">The logger.</param>
        public Collector(ILogSource source, ILogDestination destination, ILogger<Collector> logger)
        {
            _source = source;
            _destination = destination;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Try to process the files from the source.
        /// After processing the file is cleaned. This means it wil be moved either to a archive or a deadletter container.
        /// </summary>
        /// <param name="maxFileCount">Only max this number of files will be processed at once.</param>
        /// <param name="fileNameTransform">A Func to be used to generate the output file name fro the input filename.</param>
        /// <param name="sourceContentType">The <see cref="Stats.AzureCdnLogs.Common.Collect.ContentType" for the source file./></param>
        /// <param name="destinationContentType">The <see cref="Stats.AzureCdnLogs.Common.Collect.ContentType" for the destination file./></param>
        /// <param name="token">A <see cref="System.Threading.CancellationToken"/> to be used for cancelling the operation.</param>
        /// <returns>A collection of exceptions if any.</returns>
        public virtual async Task<AggregateException> TryProcessAsync(int maxFileCount, Func<string,string> fileNameTransform, ContentType sourceContentType, ContentType destinationContentType, CancellationToken token)
        {
            ConcurrentBag<Exception> exceptions = new ConcurrentBag<Exception>();
            var files = (await _source.GetFilesAsync(maxFileCount, token)).ToArray();
            var workers = Enumerable.Range(0, files.Length).Select(i => TryProcessBlobAsync(files[i], fileNameTransform, sourceContentType, destinationContentType, exceptions, token));
            await Task.WhenAll(workers);
            return exceptions.Count() > 0 ? new AggregateException(exceptions.ToArray()) : null;
        }

        private async Task TryProcessBlobAsync(Uri file, Func<string, string> fileNameTransform, ContentType sourceContentType, ContentType destinationContentType, ConcurrentBag<Exception> exceptions, CancellationToken token)
        {
            _logger.LogInformation("TryProcessAsync: {File} ", file.AbsoluteUri);
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("TryProcessAsync: The operation was cancelled.");
            }
            try
            {
                using (var lockResult = _source.TakeLockAsync(file, token).Result)
                {
                    var blobOperationToken = lockResult.BlobOperationToken.Token;
                    if (lockResult.LockIsTaken /*lockResult*/)
                    {
                        using (var inputStream = await _source.OpenReadAsync(file, sourceContentType, blobOperationToken))
                        {
                            var blobToDeadLetter = ! await VerifyStreamInternalAsync(file, sourceContentType, blobOperationToken);
                            // If verification passed continue with the rest of the action 
                            // If not just move the blob to deadletter
                            if (!blobToDeadLetter)
                            {
                                var writeOperationResult = await _destination.TryWriteAsync(inputStream, ProcessLogStream, fileNameTransform(file.Segments.Last()), destinationContentType, blobOperationToken);
                                blobToDeadLetter = writeOperationResult.OperationException != null;
                            }
                            await _source.TryCleanAsync(lockResult, onError: blobToDeadLetter, token: blobOperationToken);
                            await _source.TryReleaseLockAsync(lockResult, token: blobOperationToken);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                // Add any exceptions
                AddException(exceptions, exception);
            }
        }

        private void AddException(ConcurrentBag<Exception> exceptions, Exception e, string fileUri = "")
        {
            if (e == null)
            {
                return;
            }
            if (e is AggregateException)
            {
                foreach (Exception innerEx in ((AggregateException)e).Flatten().InnerExceptions)
                {
                    exceptions.Add(new CDNLogException(fileUri, innerEx));
                }
            }
            else
            {
                exceptions.Add(new CDNLogException(fileUri, e));
            }
        }

        /// <summary>
        /// A method to transform each line from the input stream before writing it to the output stream. It is useful for example to modify the schema of each line.
        /// </summary>
        /// <param name="line">A line from the input stream.</param>
        /// <returns>The transformed line.</returns>
        public abstract OutputLogLine TransformRawLogLine(string line);

        /// <summary>
        /// A method to validate the stream integrity before data transfer.
        /// </summary>
        /// <param name="stream">The input to validate.</param>
        /// <returns>True if the validation passed.</returns>
        public abstract Task<bool> VerifyStreamAsync(Stream stream);
      
        protected void ProcessLogStream(Stream sourceStream, Stream targetStream)
        {
            var rawLineNumber = 0;
            var targetLineNumber = 0;

            string rawLine = string.Empty;

            try
            {
                using (var sourceStreamReader = new StreamReader(sourceStream))
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    targetStreamWriter.WriteLine(OutputLogLine.Header);

                    while (!sourceStreamReader.EndOfStream)
                    {
                        rawLine = sourceStreamReader.ReadLine();
                        rawLineNumber++;

                        var transformedLine = TransformRawLogLine(rawLine);
                        if (transformedLine != null)
                        {
                            targetLineNumber++;
                            var targetLine = GetParsedModifiedLogEntry(targetLineNumber, transformedLine.ToString());
                            if (!string.IsNullOrEmpty(targetLine))
                            {
                                targetStreamWriter.Write(targetLine);
                            }
                        }
                    };

                    _logger.LogInformation("ProcessLogStream: Finished writing to the destination stream.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Failed to process raw log line number {LineNumber} with content {LineContent}",
                    rawLineNumber,
                    rawLine);
                throw;
            }
        }

        private string GetParsedModifiedLogEntry(int lineNumber, string rawLogEntry)
        {
            var parsedEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                lineNumber: lineNumber,
                line: rawLogEntry,
                onErrorAction: null);

            if (parsedEntry == null)
            {
                return null;
            }
 
            const char spaceCharacter = ' ';
            const string dashCharacter = "-";
            var stringBuilder = new StringBuilder();

            // timestamp
            stringBuilder.Append(ToUnixTimeStamp(parsedEntry.EdgeServerTimeDelivered));
            stringBuilder.Append(spaceCharacter);

            // time-taken
            stringBuilder.Append((parsedEntry.EdgeServerTimeTaken.HasValue ? parsedEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // REMOVE c-ip
            stringBuilder.Append(dashCharacter);
            stringBuilder.Append(spaceCharacter);

            // filesize
            stringBuilder.Append((parsedEntry.FileSize.HasValue ? parsedEntry.FileSize.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // s-ip
            stringBuilder.Append((parsedEntry.EdgeServerIpAddress ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // s-port
            stringBuilder.Append((parsedEntry.EdgeServerPort.HasValue ? parsedEntry.EdgeServerPort.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // sc-status
            stringBuilder.Append((parsedEntry.CacheStatusCode ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // sc-bytes
            stringBuilder.Append((parsedEntry.EdgeServerBytesSent.HasValue ? parsedEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // cs-method
            stringBuilder.Append((parsedEntry.HttpMethod ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // cs-uri-stem
            stringBuilder.Append((parsedEntry.RequestUrl ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // -
            stringBuilder.Append(dashCharacter);
            stringBuilder.Append(spaceCharacter);

            // rs-duration
            stringBuilder.Append((parsedEntry.RemoteServerTimeTaken.HasValue ? parsedEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // rs-bytes
            stringBuilder.Append((parsedEntry.RemoteServerBytesSent.HasValue ? parsedEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // c-referrer
            stringBuilder.Append((parsedEntry.Referrer ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // c-user-agent
            stringBuilder.Append((parsedEntry.UserAgent ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // customer-id
            stringBuilder.Append((parsedEntry.CustomerId ?? dashCharacter));
            stringBuilder.Append(spaceCharacter);

            // x-ec_custom-1
            stringBuilder.AppendLine((parsedEntry.CustomField ?? dashCharacter));

            return stringBuilder.ToString();
        }

        protected static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - _unixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<bool> VerifyStreamInternalAsync(Uri file, ContentType contentType , CancellationToken token)
        {
            if(token.IsCancellationRequested)
            {
                _logger.LogInformation("VerifyStreamInternalAsync: The operation was cancelled.");
                return false;
            }
            using (var stream = await _source.OpenReadAsync(file, contentType, token))
            {
                return await VerifyStreamAsync(stream);
            }
        }
    }
}
