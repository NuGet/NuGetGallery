using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class AzureStorageChecksumRecords : ChecksumRecords
    {
        public static readonly string DefaultChecksumFileName = "checksums.v1.json";
        private readonly CloudBlockBlob _checksumBlob;

        public Uri Uri
        {
            get { return _checksumBlob.Uri; }
        }

        public AzureStorageChecksumRecords(CloudBlockBlob checksumBlob)
        {
            _checksumBlob = checksumBlob;
        }

        protected override async Task<JObject> LoadJson()
        {
            try
            {
                return JObject.Parse(await _checksumBlob.DownloadTextAsync());
            }
            catch (StorageException stex)
            {
                if (stex.RequestInformation != null &&
                    stex.RequestInformation.ExtendedErrorInformation != null &&
                    (stex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.BlobNotFound ||
                     stex.RequestInformation.ExtendedErrorInformation.ErrorCode == BlobErrorCodeStrings.ContainerNotFound))
                {
                    return null; // Just didn't find the blob or container.
                }
                throw;
            }
        }

        protected override async Task SaveJson(JObject obj)
        {
            await _checksumBlob.Container.CreateIfNotExistsAsync();

            _checksumBlob.Properties.ContentType = "application/json";
            _checksumBlob.Properties.CacheControl = "no-store";

            await _checksumBlob.UploadTextAsync(obj.ToString(Formatting.None));
        }
    }
}
