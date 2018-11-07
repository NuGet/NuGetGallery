// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportWriter
    {
        private const string _contentTypeJson = "application/json";
        private readonly CloudBlobContainer _destinationContainer;

        private ILogger<ReportWriter> _logger;

        public ReportWriter(ILogger<ReportWriter> logger, CloudBlobContainer destinationContainer)
        {
            _logger = logger;
            _destinationContainer = destinationContainer;
        }

        public async Task WriteReport(string reportName, string json)
        {
            var blob = _destinationContainer.GetBlockBlobReference(reportName + ".json");
            blob.Properties.ContentType = _contentTypeJson;

            _logger.LogInformation("{ReportName}: Writing report to {ReportUri}", reportName, blob.Uri.AbsoluteUri);

            await blob.UploadTextAsync(json);
            _logger.LogInformation("{ReportName}: Wrote report to {ReportUri}", reportName, blob.Uri.AbsoluteUri);
        }
    }
}