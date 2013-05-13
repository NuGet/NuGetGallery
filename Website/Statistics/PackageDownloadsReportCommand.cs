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
    public class PackageDownloadsReportCommand : StatisticsReportCommandBase<PackageDownloadsReport>
    {
        private static readonly JsonSerializer _serializer = new JsonSerializer()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None
        };

        public PackageDownloadsReportCommand(string reportName) : base(reportName) { }

        protected override PackageDownloadsReport ParseReport(IDiagnosticsSource trace, string reportContent)
        {
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
    }
}
