// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using NuGetGallery.Configuration;

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

        public async Task<StatusViewModel> GetStatus()
        {
            return new StatusViewModel(
                IsSqlAzureAvailable(), 
                await IsAzureStorageAvailable());
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
                BlobRequestOptions options = new BlobRequestOptions();
                // Used the LocationMode.SecondaryOnly and not PrimaryThenSecondary for two reasons:
                // 1. When the primary is down and secondary is up if PrimaryThenSecondary is used there will be an extra and not needed call to the primary.
                // 2. When the primary is up the secondary status check will return the primary status instead of secondary.
                options.LocationMode = _config.ReadOnlyMode ? LocationMode.SecondaryOnly : LocationMode.PrimaryOnly;
                var tasks = _cloudStorageAvailabilityChecks.Select(s => s.IsAvailableAsync(options, operationContext : null));
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
    }
}
