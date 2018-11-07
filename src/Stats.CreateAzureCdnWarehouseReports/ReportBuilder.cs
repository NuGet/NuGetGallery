// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportBuilder
    {
        public string ReportName { get; private set; }
        public string ReportArtifactName { get; private set; }

        private ILogger<ReportBuilder> _logger;

        public ReportBuilder(ILogger<ReportBuilder> logger, string reportName)
        {
            _logger = logger;
            ReportName = reportName;
            ReportArtifactName = reportName;
        }

        public ReportBuilder(ILogger<ReportBuilder> logger, string reportName, string reportArtifactName)
        {
            _logger = logger;
            ReportName = reportName;
            ReportArtifactName = reportArtifactName;
        }
        
        public string CreateReport(DataTable table)
        {
            // Transform the data table to JSON and process it with any provided transforms
            _logger.LogInformation("{ReportName}: Creating report", ReportName);

            string json = JsonSerialize(table);

            _logger.LogInformation("{ReportName}: Created report", ReportName);

            return json;
        }

        protected virtual string JsonSerialize(DataTable table)
        {
            return JsonConvert.SerializeObject(table, Formatting.Indented);
        }
    }
}