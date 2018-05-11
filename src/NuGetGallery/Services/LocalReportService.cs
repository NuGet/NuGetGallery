// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery
{
    public class LocalReportService : IReportService
    {
        private static readonly string ProjectDirectory = HttpRuntime.AppDomainAppPath;
        private static readonly string ReportsDirectory =
            Path.Combine(ProjectDirectory, "App_Data", "Files", "Reports");

        public Task<ReportBlob> Load(string reportName)
        {
            string path = Path.Combine(ReportsDirectory, reportName);
            try
            {
                string content = File.ReadAllText(path, Encoding.UTF8);
                var lastUpdatedUtc = File.GetLastWriteTimeUtc(path);
                return Task.FromResult(new ReportBlob(content, lastUpdatedUtc));
            }
            catch (FileNotFoundException ex)
            {
                throw new ReportNotFoundException(
                    $"Report {reportName} was not found on the local filesystem.",
                    ex);
            }
        }
    }
}