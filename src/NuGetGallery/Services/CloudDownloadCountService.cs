// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public class CloudDownloadCountService : IDownloadCountService
    {
        private const string AdditionalInfoDimensionName = "AdditionalInfo";
        private const string TelemetryOriginDimensionName = "Origin";

        private const string StatsContainerName = "nuget-cdnstats";
        private const string DownloadCountBlobName = "downloads.v1.json";
        private const string TelemetryOriginForRefreshMethod = "CloudDownloadCountService.Refresh";

        private readonly ITelemetryService _telemetryService;
        private readonly Func<ICloudBlobClient> _cloudBlobClientFactory;
        private readonly ILogger<CloudDownloadCountService> _logger;

        private readonly object _refreshLock = new object();
        private bool _isRefreshing;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, long>> _downloadCounts
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>(StringComparer.OrdinalIgnoreCase);

        public CloudDownloadCountService(
            ITelemetryService telemetryService,
            Func<ICloudBlobClient> cloudBlobClientFactory,
            ILogger<CloudDownloadCountService> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _cloudBlobClientFactory = cloudBlobClientFactory ?? throw new ArgumentNullException(nameof(cloudBlobClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryGetDownloadCountForPackageRegistration(string id, out long downloadCount)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (_downloadCounts.TryGetValue(id, out var versions))
            {
                downloadCount = CalculateSum(versions);
                return true;
            }

            _telemetryService.TrackGetPackageRegistrationDownloadCountFailed(id);

            downloadCount = 0;
            return false;
        }
        
        public bool TryGetDownloadCountForPackage(string id, string version, out long downloadCount)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (_downloadCounts.TryGetValue(id, out var versions)
                && versions.TryGetValue(version, out downloadCount))
            {
                return true;
            }

            _telemetryService.TrackGetPackageDownloadCountFailed(id, version);

            downloadCount = 0;
            return false;
        }

        public async Task RefreshAsync()
        {
            bool shouldRefresh = false;
            lock (_refreshLock)
            {
                if (!_isRefreshing)
                {
                    _isRefreshing = true;
                    shouldRefresh = true;
                }
            }

            if (shouldRefresh)
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    await RefreshCoreAsync();
                    stopwatch.Stop();
                    _telemetryService.TrackDownloadJsonRefreshDuration(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds));

                }
                catch (WebException ex)
                {
                    var rethrow = true;

                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            // HTTP 412 - the blob has been updated just now
                            // don't rethrow, we'll just fetch the new data on the next refresh
                            rethrow = false;
                        }
                    }

                    if (rethrow)
                    {
                        throw;
                    }
                }
                finally
                {
                    _isRefreshing = false;
                }
            }
        }

        /// <summary>
        /// This method is added for unit testing purposes.
        /// </summary>
        protected virtual long CalculateSum(ConcurrentDictionary<string, long> versions)
        {
            return versions.Sum(kvp => kvp.Value);
        }

        /// <summary>
        /// This method is added for unit testing purposes. It can return a null stream if the blob does not exist
        /// and assumes the caller will properly dispose of the returned stream.
        /// </summary>
        protected virtual async Task<Stream> GetBlobStreamAsync()
        {
            var blob = GetBlobReference();
            return await blob.OpenReadIfExistsAsync();
        }

        private async Task RefreshCoreAsync()
        {
            try
            {
                int totalIds = 0;
                int totalVersions = 0;

                // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
                // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
                using (var blobStream = await GetBlobStreamAsync())
                {
                    if (blobStream == null)
                    {
                        return;
                    }

                    using (var jsonReader = new JsonTextReader(new StreamReader(blobStream)))
                    {
                        try
                        {
                            await jsonReader.ReadAsync();

                            while (await jsonReader.ReadAsync())
                            {
                                try
                                {
                                    if (jsonReader.TokenType == JsonToken.StartArray)
                                    {
                                        JToken record = await JToken.ReadFromAsync(jsonReader);
                                        string id = record[0].ToString().ToLowerInvariant();

                                        // The second entry in each record should be an array of versions, if not move on to next entry.
                                        // This is a check to safe guard against invalid entries.
                                        if (record.Count() == 2 && record[1].Type != JTokenType.Array)
                                        {
                                            continue;
                                        }

                                        ++totalIds;

                                        var versions = _downloadCounts.GetOrAdd(
                                            id,
                                            _ => new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase));

                                        foreach (JToken token in record)
                                        {
                                            if (token != null && token.Count() == 2)
                                            {
                                                var version = token[0].ToString();
                                                var downloadCount = token[1].ToObject<long>();
                                                ++totalVersions;

                                                if (versions.ContainsKey(version) && downloadCount < versions[version])
                                                {
                                                    _telemetryService.TrackDownloadCountDecreasedDuringRefresh(id, version, versions[version], downloadCount);
                                                }
                                                else
                                                {
                                                    versions.AddOrSet(version, downloadCount);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (JsonReaderException ex)
                                {
                                    _telemetryService.TrackException(ex, properties =>
                                    {
                                        properties.Add(TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod);
                                        properties.Add(AdditionalInfoDimensionName, "Invalid entry found in downloads.v1.json.");
                                    });
                                }
                            }
                        }
                        catch (JsonReaderException ex)
                        {
                            _telemetryService.TrackException(ex, properties =>
                            {
                                properties.Add(TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod);
                                properties.Add(AdditionalInfoDimensionName, "Data present in downloads.v1.json is invalid. Couldn't get download data.");
                            });
                        }
                    }
                }

                _telemetryService.TrackDownloadJsonTotalPackageIds(totalIds);
                _telemetryService.TrackDownloadJsonTotalPackageVersions(totalVersions);
            }
            catch (Exception ex)
            {
                _telemetryService.TrackException(ex, properties =>
                {
                    properties.Add(TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod);
                    properties.Add(AdditionalInfoDimensionName, "Unknown exception.");
                });
            }
        }

        private ISimpleCloudBlob GetBlobReference()
        {
            var blobClient = _cloudBlobClientFactory();

            var container = blobClient.GetContainerReference(StatsContainerName);
            var blob = container.GetBlobReference(DownloadCountBlobName);

            _logger.LogInformation("Cloud download statistics source: {BlobUri}", blob.Uri);

            return blob;
        }
    }
}