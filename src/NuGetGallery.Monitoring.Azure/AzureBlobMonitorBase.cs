using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring.Azure
{
    public abstract class AzureBlobMonitorBase : AzureStorageMonitorBase
    {
        public string BlobPath { get; private set; }
        public Uri BlobUrl { get; private set; }

        protected override string DefaultResourceName
        {
            get { return BlobUrl.AbsoluteUri; }
        }

        protected AzureBlobMonitorBase(string blobPath, string accountName, bool useHttps)
            : base(accountName, useHttps)
        {
            BlobPath = blobPath;
            BlobUrl = new UriBuilder(BlobEndpoint)
            {
                Path = BlobPath
            }.Uri;
        }
    }
}
