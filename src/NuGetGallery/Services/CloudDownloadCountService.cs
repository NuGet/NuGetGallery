// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public class CloudDownloadCountService : IDownloadCountService
    {
        private const string TelemetrySourcePrefix = "CloudDownloadCountService.";

        private const string AdditionalInfoDimensionName = "AdditionalInfo";
        private const string PackageIdDimensionName = "PackageId";
        private const string PackageVersionDimensionName = "PackageVersion";
        private const string TelemetryOriginDimensionName = "Origin";

        private const string DownloadCountDecreasedMetricName = TelemetrySourcePrefix + "DownloadCountDecreased";
        private const string DownloadJsonRefreshDurationMetricName = TelemetrySourcePrefix + "DownloadJsonRefreshDuration";
        private const string GetDownloadCountFailedMetricName = TelemetrySourcePrefix + "GetDownloadCountFailed";

        private const string StatsContainerName = "nuget-cdnstats";
        private const string DownloadCountBlobName = "downloads.v1.json";
        private const string TelemetryOriginForRefreshMethod = "CloudDownloadCountService.Refresh";

        private readonly ITelemetryClient _telemetryClient;
        private readonly string _connectionString;
        private readonly bool _readAccessGeoRedundant;

        private readonly object _refreshLock = new object();
        private bool _isRefreshing;

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _downloadCounts
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        public CloudDownloadCountService(ITelemetryClient telemetryClient, string connectionString, bool readAccessGeoRedundant)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            _connectionString = connectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }

        public bool TryGetDownloadCountForPackageRegistration(string id, out int downloadCount)
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

            _telemetryClient.TrackMetric(GetDownloadCountFailedMetricName, 0, GetTelemetryDimensions(id, "", TelemetrySourcePrefix + "TryGetDownloadCountForPackageRegistration"));

            downloadCount = 0;
            return false;
        }
        
        public bool TryGetDownloadCountForPackage(string id, string version, out int downloadCount)
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

            _telemetryClient.TrackMetric(GetDownloadCountFailedMetricName, 0, GetTelemetryDimensions(id, version, TelemetrySourcePrefix + "TryGetDownloadCountForPackage"));

            downloadCount = 0;
            return false;
        }

        public void Refresh()
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
                    RefreshCore();
                    stopwatch.Stop();
                    _telemetryClient.TrackMetric(DownloadJsonRefreshDurationMetricName, stopwatch.Elapsed.TotalSeconds, new Dictionary<string, string>
                        {
                            { TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod }
                        });

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
        protected virtual int CalculateSum(ConcurrentDictionary<string, int> versions)
        {
            return versions.Sum(kvp => kvp.Value);
        }

        /// <summary>
        /// This method is added for unit testing purposes. It can return a null stream if the blob does not exist
        /// and assumes the caller will properly dispose of the returned stream.
        /// </summary>
        protected virtual Stream GetBlobStream()
        {
            var blob = GetBlobReference();
            if (blob == null)
            {
                return null;
            }

            return blob.OpenRead();
        }

        private void RefreshCore()
        {
            try
            {
                // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
                // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
                using (var blobStream = GetBlobStream())
                {
                    if (blobStream == null)
                    {
                        return;
                    }

                    using (var jsonReader = new JsonTextReader(new StreamReader(blobStream)))
                    {
                        try
                        {
                            jsonReader.Read();

                            while (jsonReader.Read())
                            {
                                try
                                {
                                    if (jsonReader.TokenType == JsonToken.StartArray)
                                    {
                                        JToken record = JToken.ReadFrom(jsonReader);
                                        string id = record[0].ToString().ToLowerInvariant();

                                        // The second entry in each record should be an array of versions, if not move on to next entry.
                                        // This is a check to safe guard against invalid entries.
                                        if (record.Count() == 2 && record[1].Type != JTokenType.Array)
                                        {
                                            continue;
                                        }

                                        var versions = _downloadCounts.GetOrAdd(
                                            id,
                                            _ => new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase));

                                        foreach (JToken token in record)
                                        {
                                            if (token != null && token.Count() == 2)
                                            {
                                                var version = token[0].ToString();
                                                var downloadCount = token[1].ToObject<int>();

                                                if (versions.ContainsKey(version) && downloadCount < versions[version])
                                                {
                                                    _telemetryClient.TrackMetric(DownloadCountDecreasedMetricName, downloadCount - versions[version], GetTelemetryDimensions(id, version));
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
                                    _telemetryClient.TrackException(ex, new Dictionary<string, string>
                                    {
                                        { TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod },
                                        { AdditionalInfoDimensionName, "Invalid entry found in downloads.v1.json." }
                                    });
                                }
                            }
                        }
                        catch (JsonReaderException ex)
                        {
                            _telemetryClient.TrackException(ex, new Dictionary<string, string>
                            {
                                { TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod },
                                { AdditionalInfoDimensionName, "Data present in downloads.v1.json is invalid. Couldn't get download data." }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    { TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod },
                    { AdditionalInfoDimensionName, "Unknown exception." }
                });
            }
        }

        private Dictionary<string, string> GetTelemetryDimensions(string packageId, string packageVersion, string origin = TelemetryOriginForRefreshMethod)
        {
            return new Dictionary<string, string>
                {
                    { TelemetryOriginDimensionName, TelemetryOriginForRefreshMethod },
                    { PackageIdDimensionName, packageId },
                    { PackageVersionDimensionName, packageVersion }
                };
        }

        private CloudBlockBlob GetBlobReference()
        {
            var storageAccount = CloudStorageAccount.Parse(_connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            if (_readAccessGeoRedundant)
            {
                blobClient.DefaultRequestOptions.LocationMode = LocationMode.PrimaryThenSecondary;
            }

            var container = blobClient.GetContainerReference(StatsContainerName);
            var blob = container.GetBlockBlobReference(DownloadCountBlobName);

            if (!blob.Exists())
            {
                return null;
            }
            return blob;
        }
    }
}