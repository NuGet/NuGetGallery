using NuGetGallery.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class StatusService : IStatusService
    {
        private readonly IEntitiesContext _entities;
        private readonly IFileStorageService _fileStorageService;
        private readonly IAppConfiguration _config;

        private const string Available = "Available";
        private const string Unavailable = "Unavailable";
        private const string Unconfigured = "Unconfigured";
        private const string StatusMessageFormat = "NuGet Gallery instance {5} is {0}. SQL is {1}. Storage is {2}. Search service is {3}. Metrics service is {4}";

        private const string TestSqlQuery = "SELECT TOP(1) [Key] FROM GallerySettings WITH (NOLOCK)";

        public StatusService(
            IEntitiesContext entities,
            IFileStorageService fileStorageService,
            IAppConfiguration config)
        {
            _entities = entities;
            _fileStorageService = fileStorageService;
            _config = config;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Just want to log the exception and return the appropriate HTTPStatusCode")]
        public async Task<ActionResult> GetStatus()
        {
            bool sqlAzureAvailable =  IsSqlAzureAvailable();
            bool? storageAvailable = await IsAzureStorageAvailable();
            bool? searchServiceAvailable = await IsSearchServiceAvailable();
            bool? metricsServiceAvailable = await IsMetricsServiceAvailable();

            bool galleryServiceAvailable =
                sqlAzureAvailable
                && (!storageAvailable.HasValue || storageAvailable.Value) // null == true for this condition.
                && (!searchServiceAvailable.HasValue || searchServiceAvailable.Value)
                && (!metricsServiceAvailable.HasValue || metricsServiceAvailable.Value);

            return new HttpStatusCodeWithBodyResult(AvailabilityStatusCode(galleryServiceAvailable),
                String.Format(CultureInfo.InvariantCulture,
                    StatusMessageFormat,
                    AvailabilityMessage(galleryServiceAvailable),
                    AvailabilityMessage(sqlAzureAvailable),
                    AvailabilityMessage(storageAvailable),
                    AvailabilityMessage(searchServiceAvailable),
                    AvailabilityMessage(metricsServiceAvailable),
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

        private async Task<bool?> IsAzureStorageAvailable()
        {
            if (_config == null || _config.StorageType != StorageType.AzureStorage)
            {
                return null;
            }

            bool storageAvailable = false;
            try
            {
                // Check Storage Availability
                storageAvailable = await _fileStorageService.FileExistsAsync(Constants.DownloadsFolderName, "nuget.exe");
            }
            catch (Exception ex)
            {
                // Could catch Azure's StorageException alone. But, the status page is not supposed to throw at any cost
                // And, catching StorageException will compromise the IFileStorageService abstraction

                QuietLog.LogHandledException(ex);
            }

            return storageAvailable;
        }

        private async Task<bool?> IsSearchServiceAvailable()
        {
            if (_config == null || _config.ServiceDiscoveryUri == null)
            {
                return null;
            }

            return await IsGetSuccessful(_config.ServiceDiscoveryUri);
        }

        private async Task<bool?> IsMetricsServiceAvailable()
        {
            if (_config == null || _config.MetricsServiceUri == null)
            {
                return null;
            }

            return await IsGetSuccessful(_config.MetricsServiceUri);
        }

        private async Task<bool> IsGetSuccessful(Uri uri)
        {
            using(var httpClient = new HttpClient())
            {
                // This method does not throw for unsuccessful responses
                var responseMessage = await httpClient.GetAsync(uri);
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
