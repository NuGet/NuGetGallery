using Lucene.Net.Index;
using Lucene.Net.Search;
using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Net;
using System.Text;

namespace NuGet.Indexing
{
    public static class MetadataImpl
    {
        public static void Access(IOwinContext context, string tenantId, SecureSearcherManager searcherManager, string connectionString, int accessDuration)
        {
            string relativeUri = context.Request.Path.ToString();

            string[] parts = relativeUri.Split('/');

            if (!(parts.Length >= 4))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            if (!parts[1].Equals("resource"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            string containerName = parts[2];
            string blobName = string.Join("/", parts, 3, parts.Length - 3);

            if (IsAuthorized(searcherManager, tenantId, containerName, blobName))
            {
                Redirect(context, containerName, blobName, connectionString, accessDuration);
            }
            else
            {
                context.Response.Write("unauthorized");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
        }

        static bool IsAuthorized(SecureSearcherManager searcherManager, string tenantId, string containerName, string blobName)
        {
            searcherManager.MaybeReopen();

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                Filter filter = searcherManager.GetFilter(tenantId, new string[] { "http://schema.nuget.org/schema#ApiAppPackage", "http://schema.nuget.org/schema#CatalogInfrastructure" });
                string relativePath = string.Format("/{0}/{1}", containerName, blobName);
                Query query = new TermQuery(new Term("StoragePath", relativePath));
                TopDocs topDocs = searcher.Search(query, 1);
                return (topDocs.TotalHits > 0);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        static void Redirect(IOwinContext context, string containerName, string blobName, string connectionString, int accessDuration)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            SharedAccessBlobPolicy sharedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddSeconds(accessDuration),
                Permissions = SharedAccessBlobPermissions.Read
            };

            string sharedAccessSignature = blob.GetSharedAccessSignature(sharedPolicy);

            context.Response.Redirect(blob.Uri + sharedAccessSignature);
        }
    }
}
