using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Http
{
    public abstract class NuGetApiController : ApiController
    {
        public ServiceHost Host { get; set; }
        public NuGetApiService Service { get; set; }
        public ILifetimeScope Container { get; set; }

        public NuGetApiController()
        {
        }

        protected TransferBlobResult TransferBlob(ICloudBlob blob)
        {
            return new TransferBlobResult(blob);
        }

        protected Task<TransferBlobResult> TransferBlob(string blobUri)
        {
            return TransferBlob(new Uri(blobUri));
        }

        protected async Task<TransferBlobResult> TransferBlob(Uri blobUri)
        {
            var blob = await Service.Storage.Primary.Blobs.Client.GetBlobReferenceFromServerAsync(blobUri);
            return TransferBlob(blob);
        }
    }
}
