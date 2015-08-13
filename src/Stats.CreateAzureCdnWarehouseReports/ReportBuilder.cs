// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class ReportBuilder
    {
        public ReportBuilder(string reportName)
        {
            ReportName = reportName;
            ReportArtifactName = reportName;
        }

        public ReportBuilder(string reportName, string reportArtifactName)
        {
            ReportName = reportName;
            ReportArtifactName = reportArtifactName;
        }

        public string ReportName { get; private set; }
        public string ReportArtifactName { get; private set; }

        public string CreateReport(DataTable table)
        {
            // Transform the data table to JSON and process it with any provided transforms
            Trace.TraceInformation("{0}: Creating report", ReportName);

            string json = JsonSerialize(table);

            Trace.TraceInformation("{0}: Created report", ReportName);

            return json;
        }

        protected virtual string JsonSerialize(DataTable table)
        {
            return JsonConvert.SerializeObject(table, Formatting.Indented);
        }
    }
}