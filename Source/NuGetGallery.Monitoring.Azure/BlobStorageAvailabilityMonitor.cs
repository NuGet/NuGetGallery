using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Monitoring.Azure
{
    public class BlobStorageAvailabilityMonitor : AzureBlobMonitorBase
    {
        public BlobStorageAvailabilityMonitor(string blobPath, string accountName, bool useHttps) : base(blobPath, accountName, useHttps) { }

        protected override Task Invoke()
        {
            return Task.WhenAll(
                new HttpMonitor(BlobUrl) { Method = "HEAD" }.Invoke(Reporter, CancelToken));
        }
    }
}
