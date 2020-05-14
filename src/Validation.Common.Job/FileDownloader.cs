// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Jobs.Validation
{
    public class FileDownloader : IFileDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ICommonTelemetryService _telemetryService;
        private readonly FileDownloaderConfiguration _configuration;
        private readonly ILogger<FileDownloader> _logger;

        public FileDownloader(
            HttpClient httpClient,
            ICommonTelemetryService telemetryService,
            IOptionsSnapshot<FileDownloaderConfiguration> downloaderConfigurationAccessor,
            ILogger<FileDownloader> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            if (downloaderConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(downloaderConfigurationAccessor));
            }
            if (downloaderConfigurationAccessor.Value.BufferSize <= 0)
            {
                throw new ArgumentException($"{nameof(downloaderConfigurationAccessor.Value.BufferSize)} cannot be less than 1", nameof(downloaderConfigurationAccessor));
            }
            _configuration = downloaderConfigurationAccessor.Value;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FileDownloadResult> DownloadAsync(Uri fileUri, CancellationToken cancellationToken)
        {
            return await DownloadInternalAsync(fileUri, expectedFileSize: null, cancellationToken: cancellationToken);
        }

        public async Task<FileDownloadResult> DownloadExpectedFileSizeAsync(Uri fileUri, long expectedFileSize, CancellationToken cancellationToken)
        {
            return await DownloadInternalAsync(fileUri, expectedFileSize, cancellationToken);
        }

        private async Task<FileDownloadResult> DownloadInternalAsync(Uri fileUri, long? expectedFileSize, CancellationToken cancellationToken)
        {
            if (fileUri == null)
            {
                throw new ArgumentNullException(nameof(fileUri));
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException(nameof(cancellationToken));
            }

            _logger.LogInformation("Attempting to download file from {FileUri}...", fileUri);

            Stream fileStream = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Download the file from the network to a temporary file.
                using (var response = await _httpClient.GetAsync(fileUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    _logger.LogInformation(
                        "Received response {StatusCode}: {ReasonPhrase} of type {ContentType} for request {FileUri}",
                        response.StatusCode,
                        response.ReasonPhrase,
                        response.Content?.Headers.ContentType,
                        fileUri);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return FileDownloadResult.NotFound();
                    }
                    else if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new InvalidOperationException($"Expected status code {HttpStatusCode.OK} for file download, actual: {response.StatusCode}");
                    }

                    using (var networkStream = await response.Content.ReadAsStreamAsync())
                    using (cancellationToken.Register(() => networkStream.Close()))
                    {
                        fileStream = FileStreamUtility.GetTemporaryFile();

                        if (expectedFileSize.HasValue)
                        {
                            var result = await networkStream.CopyToAsync(
                                fileStream,
                                _configuration.BufferSize,
                                expectedFileSize.Value,
                                cancellationToken);

                            if (result.BytesWritten != expectedFileSize.Value || result.PartialRead)
                            {
                                fileStream.Dispose();

                                _logger.LogError(
                                    "File has unexpected size for request {FileUri}. " +
                                    "Expected: {ExpectedFileSizeInBytes}. " +
                                    "Actual: {ActualFileSizeInBytes}",
                                    fileUri,
                                    expectedFileSize,
                                    result.PartialRead ? ">" + result.BytesWritten : result.BytesWritten.ToString());

                                return FileDownloadResult.UnexpectedFileSize();
                            }
                        }
                        else
                        {
                            await networkStream.CopyToAsync(fileStream, _configuration.BufferSize, cancellationToken);
                        }
                    }
                }

                fileStream.Position = 0;

                stopwatch.Stop();

                _logger.LogInformation(
                    "Downloaded {FileSizeInBytes} bytes in {DownloadElapsedTime} seconds for request {FileUri}",
                    fileStream.Length,
                    stopwatch.Elapsed.TotalSeconds,
                    fileUri);

                _telemetryService.TrackFileDownloaded(fileUri, stopwatch.Elapsed, fileStream.Length);

                return FileDownloadResult.Ok(fileStream);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Exception thrown when trying to download package from {FileUri}",
                    fileUri);

                fileStream?.Dispose();

                throw;
            }
        }
    }
}
