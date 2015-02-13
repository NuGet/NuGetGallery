using Microsoft.Owin;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace NuGet.Services.Gateway
{
    public static class ServiceImpl
    {
        static string connectionString = "";

        public async static Task Get(IOwinContext context)
        {
            //  (1) Authenticate (FaceBook)
            //  (2) Authorize tenant claim to blob
            //  (3) gen sas and redirect

            string relativeUri = context.Request.Path.ToString();

            if (relativeUri == "/")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                return;
            }

            string[] parts = relativeUri.Split('/');

            string containerName = parts[parts.Length - 2];
            string blobName = parts[parts.Length - 1];

            CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            SharedAccessBlobPolicy sharedPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddSeconds(10),
                Permissions = SharedAccessBlobPermissions.Read
            };

            string sharedAccessSignature = blob.GetSharedAccessSignature(sharedPolicy);

            context.Response.Redirect(blob.Uri + sharedAccessSignature);

            /*
            PathString relativeUri = context.Request.Path;

            //QueryString queryString = new QueryString("x=1");
            //relativeUri.Add(queryString);

            Uri blobAddress = new Uri(BaseAddress.Trim('/') + relativeUri.ToUriComponent());

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(blobAddress);

            if (response.IsSuccessStatusCode)
            {
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
                context.Response.ContentLength = response.Content.Headers.ContentLength;
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                //byte[] buf = await response.Content.ReadAsByteArrayAsync();
                //await context.Response.WriteAsync(buf);

                CancellationTokenSource cts = new CancellationTokenSource();
                Stream stream = await response.Content.ReadAsStreamAsync();

                byte[] buf = new byte[128 * 1024];
                int len = 0;
                do
                {
                    len = await stream.ReadAsync(buf, 0, buf.Length);
                    if (len > 0)
                    {
                        await context.Response.WriteAsync(buf, 0, len, cts.Token);
                    }
                }
                while(len > 0);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            */
        }
    }
}