using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Internal.Web.Utils;
using Newtonsoft.Json;
using NuGetGallery.Commands;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadsReportQuery : Query<Task<PackageDownloadsReport>>
    {
        private static readonly JsonSerializer _serializer = new JsonSerializer()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        public IFileStorageService StorageService { get; set; }
        public IDiagnosticsService Diagnostics { get; set; }

        public string ReportName { get; private set; }

        public PackageDownloadsReportQuery(string reportName)
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

            // Parse it into the object
            IEnumerable<PackageDownloadsReportEntry> entries;
            using (var reader = new JsonTextReader(new StringReader(reportContent)))
            {
                try
                {
                    entries = _serializer.Deserialize<IEnumerable<PackageDownloadsReportEntry>>(reader);
                }
                catch (JsonException ex)
                {
                    trace.Error(String.Format("Error loading {0} report. Exception: {1}", ReportNames.RecentPackageDownloads, ex.ToString()));
                    entries = null;
                }
            }

            // Return the report!
            return new PackageDownloadsReport(entries ?? Enumerable.Empty<PackageDownloadsReportEntry>());
        }

        // Properly implemented equality makes tests easier!
        public override bool Equals(object obj)
        {
            PackageDownloadsReportQuery other = obj as PackageDownloadsReportQuery;
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
