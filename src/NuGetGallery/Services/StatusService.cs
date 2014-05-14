using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class StatusService : IStatusService
    {
        private readonly IEntitiesContext _entities;
        private readonly IFileStorageService _fileStorageService;

        private const string Available = "Available";
        private const string Unavailable = "Unavailable";
        private const string StatusMessageFormat = "NuGet Gallery service is {0}. SQL Azure is {1}. Storage is {2}";

        private const string TestSqlQuery = "SELECT TOP(1) [Key] FROM GallerySettings WITH (NOLOCK)";

        public StatusService(
            IEntitiesContext entities,
            IFileStorageService fileStorageService)
        {
            _entities = entities;
            _fileStorageService = fileStorageService;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Just want to log the exception and return the appropriate HTTPStatusCode")]
        public async Task<ActionResult> GetStatus()
        {
            bool sqlAzureAvailable =  false;
            bool storageAvailable =  false;

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

            bool galleryServiceAvailable = sqlAzureAvailable && storageAvailable;

            return new HttpStatusCodeResult(AvailabilityStatusCode(galleryServiceAvailable),
                String.Format(CultureInfo.InvariantCulture,
                    StatusMessageFormat,
                    AvailabilityMessage(galleryServiceAvailable),
                    AvailabilityMessage(sqlAzureAvailable),
                    AvailabilityMessage(storageAvailable)));
        }

        private static string AvailabilityMessage(bool available)
        {
            return available ? Available : Unavailable;
        }

        private static HttpStatusCode AvailabilityStatusCode(bool available)
        {
            return available ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        }
    }
}