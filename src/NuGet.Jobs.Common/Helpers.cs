using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public static class StorageHelpers
    {
        private static readonly string PackageBackupsDirectory = "packages";
        private static readonly string PackageBlobNameFormat = "{0}.{1}.nupkg";
        private static readonly string PackageBackupBlobNameFormat = PackageBackupsDirectory + "/{0}/{1}/{2}.nupkg";
        private const string ContentTypeJson = "application/json";

        public static string GetPackageBlobName(string id, string version)
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                PackageBlobNameFormat,
                id,
                version).ToLowerInvariant();
        }

        public static string GetPackageBackupBlobName(string id, string version, string hash)
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                PackageBackupBlobNameFormat,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                WebUtility.UrlEncode(hash));
        }

        public static CloudBlobDirectory GetBlobDirectory(CloudStorageAccount account, string path)
        {
            var client = account.CreateCloudBlobClient();
            client.DefaultRequestOptions = new BlobRequestOptions()
            {
                ServerTimeout = TimeSpan.FromMinutes(5)
            };

            string[] segments = path.Split('/');
            string containerName;
            string prefix;

            if (segments.Length < 2)
            {
                // No "/" segments, so the path is a container and the catalog is at the root...
                containerName = path;
                prefix = String.Empty;
            }
            else
            {
                // Found "/" segments, but we need to get the first segment to use as the container...
                containerName = segments[0];
                prefix = String.Join("/", segments.Skip(1)) + "/";
            }

            var container = client.GetContainerReference(containerName);
            var dir = container.GetDirectoryReference(prefix);
            return dir;
        }

        public static async Task UploadJsonBlob(CloudBlobContainer container, string blobName, string content)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = ContentTypeJson;
            await blob.UploadTextAsync(content);
        }
    }

    public static class ArgCheck
    {
        public static void Require(string value, string name)
        {
            if (String.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void Require(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}

namespace System.Data.SqlClient
{
    public static class SqlConnectionStringBuilderExtensions
    {
        public static Task<SqlConnection> ConnectTo(this SqlConnectionStringBuilder self)
        {
            return ConnectTo(self.ConnectionString);
        }

        private static async Task<SqlConnection> ConnectTo(string connection)
        {
            var c = new SqlConnection(connection);
            await c.OpenAsync().ConfigureAwait(continueOnCapturedContext: false);
            return c;
        }
    }
}

