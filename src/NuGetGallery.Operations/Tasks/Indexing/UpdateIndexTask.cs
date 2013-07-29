using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery.Operations.Model;
using Dapper;
using NuGetGallery.Operations.Indexing;

namespace NuGetGallery.Operations.Tasks.Indexing
{
    [Command("updateindex", "Updates the Lucene index with the latest data from the database", AltName="upidx")]
    public class UpdateIndexTask : DatabaseAndStorageTask
    {
        public override void ExecuteCommand()
        {
            var startTime = DateTime.UtcNow; // Capture current time to write back to IndexMetadata later
            
            // Retrieve the metadata blob
            var blobs = StorageAccount.CreateCloudBlobClient();
            var indexMetadata = GetIndexMetadata(blobs);

            // Retrieve data from the packages table
            IList<dynamic> packages = null;
            WithConnection((c, db) =>
            {
                if (indexMetadata != null)
                {
                    Log.Info("Requesting packages modified after {0}", indexMetadata.LastUpdatedUtc);
                    packages = c.Query(
                        "SELECT * FROM Packages WHERE LastUpdated >= @lastUpdated",
                        new { lastUpdated = indexMetadata.LastUpdatedUtc })
                        .ToList();
                }
                else
                {
                    Log.Info("Requesting ALL packages (this may take a while)", indexMetadata.LastUpdatedUtc);
                    packages = c.Query("SELECT * FROM Packages").ToList();
                }
            });
            Log.Info("Found {0} packages to update", packages.Count);

            // Open the index
            using (PackageIndex index = PackageIndex.Open(StorageAccount))
            {
                // Inject the packages
                index.AddOrUpdate(packages);
            }
        }

        private IndexMetadata GetIndexMetadata(CloudBlobClient blobs)
        {
            Log.Info("Fetching Index Metadata");
            return blobs.ReadJsonBlob<IndexMetadata>("lucene", "metadata.json");
        }
    }
}
