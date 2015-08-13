// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using Newtonsoft.Json.Linq;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class DownloadsByPackageIdReportBuilder
        : ReportBuilder
    {
        public DownloadsByPackageIdReportBuilder(string reportName, string reportArtifactName)
            : base(reportName, reportArtifactName)
        {
        }

        protected override string JsonSerialize(DataTable table)
        {
            var jObject = MakeReportJson(table);
            return jObject.ToString();
        }

        public static JObject MakeReportJson(DataTable data)
        {
            var report = new JObject();
            
            foreach (DataRow row in data.Rows)
            {
                var packageVersion = (string)row[1];
                var packageDownloads = (int)row[2];
                
                report.Add(packageVersion, new JValue(packageDownloads));
            }

            return report;
        }
    }
}