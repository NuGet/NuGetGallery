// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class StatusService : IStatusService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly IEntitiesContext _entities;
        private readonly List<ICloudStorageStatusDependency> _cloudStorageAvailabilityChecks;
        private readonly IAppConfiguration _config;

        private const string Available = "Available";
        private const string Unavailable = "Unavailable";
        private const string Unconfigured = "Unconfigured";
        private const string StatusMessageFormat = "NuGet Gallery instance {3} is {0}. SQL is {1}. Storage is {2}.";

        private const string TestSqlQuery = "SELECT TOP(1) [Key] FROM GallerySettings WITH (NOLOCK)";

        public StatusService(
            IEntitiesContext entities,
            IEnumerable<ICloudStorageStatusDependency> cloudStorageAvailabilityChecks,
            IAppConfiguration config)
        {
            _entities = entities;
            _cloudStorageAvailabilityChecks = cloudStorageAvailabilityChecks.ToList();
            _config = config;
        }

        public async Task<ActionResult> GetStatus()
        {
            bool sqlAzureAvailable =  IsSqlAzureAvailable();
            bool? storageAvailable = await IsAzureStorageAvailable();

            bool galleryServiceAvailable =
                sqlAzureAvailable
                && (!storageAvailable.HasValue || storageAvailable.Value); // null == true for this condition.

            return new HttpStatusCodeWithBodyResult(AvailabilityStatusCode(galleryServiceAvailable),
                String.Format(CultureInfo.InvariantCulture,
                    StatusMessageFormat,
                    AvailabilityMessage(galleryServiceAvailable),
                    AvailabilityMessage(sqlAzureAvailable),
                    AvailabilityMessage(storageAvailable),
                    HostMachine.Name));
        }

        private bool IsSqlAzureAvailable()
        {
            bool sqlAzureAvailable = false;
            try
            {
                // Check SQL Azure Availability
                var dbContext = (DbContext)_entities;
                var result = dbContext.Database.SqlQuery<int>(TestSqlQuery).ToList();
                sqlAzureAvailable = result.Count > 0;
            }
            catch (Exception ex)
            {
                // Could catch SQLException alone. But, the status page is not supposed to throw at any cost

                QuietLog.LogHandledException(ex);
            }

            return sqlAzureAvailable;
        }

        internal async Task<bool?> IsAzureStorageAvailable()
        {
            if (_config == null || _config.StorageType != StorageType.AzureStorage)
            {
                return null;
            }

            bool storageAvailable = false;
            try
            {
                // Check Storage Availability
                // Used the LocationMode.SecondaryOnly and not PrimaryThenSecondary for two reasons:
                // 1. When the primary is down and secondary is up if PrimaryThenSecondary is used there will be an extra and not needed call to the primary.
                // 2. When the primary is up the secondary status check will return the primary status instead of secondary.
                var locationMode = _config.ReadOnlyMode ? CloudBlobLocationMode.SecondaryOnly : CloudBlobLocationMode.PrimaryOnly;
                var tasks = _cloudStorageAvailabilityChecks.Select(s => s.IsAvailableAsync(locationMode));
                var eachAvailable = await Task.WhenAll(tasks);
                storageAvailable = eachAvailable.All(a => a);
            }
            catch (Exception ex)
            {
                // Could catch Azure's StorageException alone. But, the status page is not supposed to throw at any cost
                // And, catching StorageException will compromise the IFileStorageService abstraction

                QuietLog.LogHandledException(ex);
            }

            return storageAvailable;
        }

        private async Task<bool> IsGetSuccessful(Uri uri)
        {
            // This method does not throw for unsuccessful responses
            using (var responseMessage = await _httpClient.GetAsync(uri))
            {
                return responseMessage.IsSuccessStatusCode;
            }
        }

        private static string AvailabilityMessage(bool? available)
        {
            return
                !available.HasValue ?
                    Unconfigured :
                    (available.Value ? Available : Unavailable);
        }

        private static HttpStatusCode AvailabilityStatusCode(bool available)
        {
            return available ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        }
    }
}
