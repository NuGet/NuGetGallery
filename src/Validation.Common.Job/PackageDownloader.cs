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
    public class PackageDownloader : IFileDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly ICommonTelemetryService _telemetryService;
        private readonly PackageDownloaderConfiguration _configuration;
        private readonly ILogger<PackageDownloader> _logger;

        public PackageDownloader(
            HttpClient httpClient,
            ICommonTelemetryService telemetryService,
            IOptionsSnapshot<PackageDownloaderConfiguration> downloaderConfigurationAccessor,
            ILogger<PackageDownloader> logger)
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

        public async Task<Stream> DownloadAsync(Uri packageUri, CancellationToken cancellationToken)
        {
            if (packageUri == null)
            {
                throw new ArgumentNullException(nameof(packageUri));
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException(nameof(cancellationToken));
            }

            _logger.LogInformation("Attempting to download package from {PackageUri}...", packageUri);

            Stream packageStream = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Download the package from the network to a temporary file.
                using (var response = await _httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    _logger.LogInformation(
                        "Received response {StatusCode}: {ReasonPhrase} of type {ContentType} for request {PackageUri}",
                        response.StatusCode,
                        response.ReasonPhrase,
                        response.Content.Headers.ContentType,
                        packageUri);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new InvalidOperationException($"Expected status code {HttpStatusCode.OK} for package download, actual: {response.StatusCode}");
                    }

                    using (var networkStream = await response.Content.ReadAsStreamAsync())
                    using (cancellationToken.Register(() => networkStream.Close()))
                    {
                        packageStream = FileStreamUtility.GetTemporaryFile();

                        await networkStream.CopyToAsync(packageStream, _configuration.BufferSize, cancellationToken);
                    }
                }

                packageStream.Position = 0;

                stopwatch.Stop();

                _logger.LogInformation(
                    "Downloaded {PackageSizeInBytes} bytes in {DownloadElapsedTime} seconds for request {PackageUri}",
                    packageStream.Length,
                    stopwatch.Elapsed.TotalSeconds,
                    packageUri);

                _telemetryService.TrackPackageDownloaded(packageUri, stopwatch.Elapsed, packageStream.Length);

                return packageStream;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    Error.FailedToDownloadPackage,
                    e,
                    "Exception thrown when trying to download package from {PackageUri}",
                    packageUri);

                packageStream?.Dispose();

                throw;
            }
        }
    }
}
