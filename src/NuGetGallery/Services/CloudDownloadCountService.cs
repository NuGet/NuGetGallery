// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private const string StatsContainerName = "nuget-cdnstats";
        private const string DownloadCountBlobName = "downloads.v1.json";
        private const string TelemetryOriginForRefreshMethod = "CloudDownloadCountService.Refresh";

        private readonly string _connectionString;
        private readonly bool _readAccessGeoRedundant;

        private readonly object _refreshLock = new object();
        private bool _isRefreshing;

        private readonly IDictionary<string, IDictionary<string, int>> _downloadCounts = new Dictionary<string, IDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        
        public DateTime LastRefresh { get; protected set; }

        public CloudDownloadCountService(string connectionString, bool readAccessGeoRedundant)
        {
            _connectionString = connectionString;
            _readAccessGeoRedundant = readAccessGeoRedundant;
        }
        
        public bool TryGetDownloadCountForPackageRegistration(string id, out int downloadCount)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            id = id.ToLowerInvariant();

            if (_downloadCounts.ContainsKey(id))
            {
                downloadCount = _downloadCounts[id].Sum(kvp => kvp.Value);
                return true;
            }

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

            id = id.ToLowerInvariant();
            version = version.ToLowerInvariant();

            if (_downloadCounts.ContainsKey(id))
            {
                if (_downloadCounts[id].ContainsKey(version))
                {
                    downloadCount = _downloadCounts[id][version];
                    return true;
                }
            }

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
                    RefreshCore();
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
                    LastRefresh = DateTime.UtcNow;
                }
            }
        }

        private void RefreshCore()
        {
            try
            {
                var blob = GetBlobReference();
                if (blob == null)
                {
                    return;
                }

                // The data in downloads.v1.json will be an array of Package records - which has Id, Array of Versions and download count.
                // Sample.json : [["AutofacContrib.NSubstitute",["2.4.3.700",406],["2.5.0",137]],["Assman.Core",["2.0.7",138]]....
                using (var jsonReader = new JsonTextReader(new StreamReader(blob.OpenRead())))
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

                                    if (!_downloadCounts.ContainsKey(id))
                                    {
                                        _downloadCounts.Add(id, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                                    }
                                    var versions = _downloadCounts[id];

                                    foreach (JToken token in record)
                                    {
                                        if (token != null && token.Count() == 2)
                                        {
                                            string version = token[0].ToString().ToLowerInvariant();
                                            versions[version] = token[1].ToObject<int>();
                                        }
                                    }
                                }
                            }
                            catch (JsonReaderException ex)
                            {
                                Telemetry.TrackException(ex, new Dictionary<string, string>
                                {
                                    { "Origin", TelemetryOriginForRefreshMethod },
                                    { "AdditionalInfo", "Invalid entry found in downloads.v1.json." }
                                });
                            }
                        }
                    }
                    catch (JsonReaderException ex)
                    {
                        Telemetry.TrackException(ex, new Dictionary<string, string>
                        {
                            { "Origin", TelemetryOriginForRefreshMethod },
                            { "AdditionalInfo", "Data present in downloads.v1.json is invalid. Couldn't get download data." }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Telemetry.TrackException(ex, new Dictionary<string, string>
                {
                    { "Origin", TelemetryOriginForRefreshMethod },
                    { "AdditionalInfo", "Unknown exception." }
                });
            }
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