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
        
        public Collector()
        { }

        /// <summary>
        /// .ctor for the Collector
        /// </summary>
        /// <param name="source">The source of the Collector.</param>
        /// <param name="destination">The destination for the collector.</param>
        public Collector(ILogSource source, ILogDestination destination)
        {
            _source = source;
            _destination = destination;
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
            try
            {
                var files = await _source.GetFilesAsync(maxFileCount, token);
                var parallelResult = Parallel.ForEach(files, (file) =>
                {
                    if(token.IsCancellationRequested)
                    {
                        return;
                    }
                    var lockResult = _source.TakeLockAsync(file, token).Result;
                    if (lockResult.Item1 /*lockResult*/)
                    {
                        using (var inputStream = _source.OpenReadAsync(file, sourceContentType, token).Result)
                        {
                            var writeAction = VerifyStreamInternalAsync(file, sourceContentType, token).
                            ContinueWith(t =>
                            {
                                //if validation failed clean the file to not continue processing over and over 
                                if (!t.Result)
                                {
                                    throw new ApplicationException($"File {file} failed validation.");
                                }
                                _destination.WriteAsync(inputStream, ProcessLogStream, fileNameTransform(file.Segments.Last()), destinationContentType, token).Wait();
                            }).
                            ContinueWith(t =>
                            {
                                AddException(exceptions, t.Exception);
                                return _source.CleanAsync(file, onError: t.IsFaulted, token: token).Result;
                            }).
                            ContinueWith(t =>
                            {
                                AddException(exceptions, t.Exception);
                                return _source.ReleaseLockAsync(file, token).Result;
                            }).
                            ContinueWith(t =>
                            {
                                AddException(exceptions, t.Exception);
                                return t.Result;
                            }).Result;
                        }
                    }
                    //log any exceptions from the renewlease task if faulted
                    //if the task is still running at this moment any future failure would not matter 
                    if(lockResult.Item2 != null && lockResult.Item2.IsFaulted)
                    {
                        AddException(exceptions, lockResult.Item2.Exception);
                    }
                });
            }
            catch (Exception e)
            {
                AddException(exceptions, e);
            }
            return exceptions.Count() > 0 ? new AggregateException(exceptions.ToArray()) : null;
        }

        private void AddException(ConcurrentBag<Exception> exceptions, Exception e)
        {
            if(e == null)
            {
                return;
            }
            if (e is AggregateException)
            {
                foreach (Exception innerEx in ((AggregateException)e).Flatten().InnerExceptions)
                {
                    exceptions.Add(innerEx);
                }
            }
            else
            {
                exceptions.Add(e);
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
            using (var sourceStreamReader = new StreamReader(sourceStream))
            using (var targetStreamWriter = new StreamWriter(targetStream))
            {
                targetStreamWriter.WriteLine(OutputLogLine.Header);
                var lineNumber = 0;
                while (!sourceStreamReader.EndOfStream)
                {
                    var rawLogLine = TransformRawLogLine(sourceStreamReader.ReadLine());
                    if (rawLogLine != null)
                    {
                        lineNumber++;
                        var logLine = GetParsedModifiedLogEntry(lineNumber, rawLogLine.ToString());
                        if (!string.IsNullOrEmpty(logLine))
                        {
                            targetStreamWriter.Write(logLine);
                        }
                    }
                };
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
                return false;
            }
            using (var stream = await _source.OpenReadAsync(file, contentType, token))
            {
                return await VerifyStreamAsync(stream);
            }
        }
    }
}
