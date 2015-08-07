// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportWriter
    {
        private const string _contentTypeJson = "application/json";
        private readonly CloudBlobContainer _destinationContainer;

        public ReportWriter(CloudBlobContainer destinationContainer)
        {
            _destinationContainer = destinationContainer;
        }

        public async Task WriteReport(string reportName, string json)
        {
            var blob = _destinationContainer.GetBlockBlobReference("popularity/" + reportName + ".json");
            blob.Properties.ContentType = _contentTypeJson;

            Trace.TraceInformation("{0}: Writing report to {1}", reportName, blob.Uri.AbsoluteUri);

            await blob.UploadTextAsync(json);
            Trace.TraceInformation("{0}: Wrote report to {1}", reportName, blob.Uri.AbsoluteUri);
        }
    }
}