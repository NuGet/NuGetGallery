using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Internal.Web.Utils;
using NuGetGallery.Commands;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Statistics
{
    public abstract class StatisticsReportQueryBase<TReportType> : Query<Task<TReportType>>
    {
        public IFileStorageService StorageService { get; set; }
        public IDiagnosticsService Diagnostics { get; set; }

        public string ReportName { get; private set; }

        public StatisticsReportQueryBase(string reportName)
        {
            ReportName = reportName;
        }

        public override async Task<PackageDownloadsReport> Execute()
        {
            var trace = Diagnostics == null ? new NullDiagnosticsSource() : Diagnostics.GetSource("PackageDownloadsReportQuery");

            // Load the report from file storage
            string reportContent;
            var stream = await StorageService.GetFileAsync("stats", "popularity/" + ReportName.ToLowerInvariant() + ".json");
            if (stream == null)
            {
                return null;
            }

            // The reader will close the stream.
            using (var reader = new StreamReader(stream))
            {
                reportContent = await reader.ReadToEndAsync();
            }

            return ParseReport(trace, reportContent);
        }

        protected abstract PackageDownloadsReport ParseReport(IDiagnosticsSource trace, string reportContent);

        // Properly implemented equality makes tests easier!
        public override bool Equals(object obj)
        {
            StatisticsReportQueryBase<TReportType> other = obj as StatisticsReportQueryBase<TReportType>;
            return other != null &&
                   Equals(StorageService, other.StorageService) &&
                   Equals(Diagnostics, other.Diagnostics) &&
                   String.Equals(ReportName, other.ReportName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.Start()
                .Add(StorageService)
                .Add(Diagnostics)
                .Add(ReportName)
                .CombinedHash;
        }
    }
}