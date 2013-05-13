using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGetGallery.Commands;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Statistics
{
    public class PackageDownloadDetailReportCommand : StatisticsReportCommandBase<DownloadStatisticsReport>
    {
        public string Id { get; private set; }
        public string Version { get; private set; }

        public PackageDownloadDetailReportCommand(string id) : this(id, null) { }

        public PackageDownloadDetailReportCommand(string id, string version) : base(BuildReportName(id, version))
        {
            Id = id;
            Version = version;
        }

        private static string BuildReportName(string id, string version)
        {
            return String.IsNullOrEmpty(version) ?
                ReportNames.DownloadsForPackage(id) :
                ReportNames.DownloadsForPackageVersion(id, version);
        }
    }

    public class PackageDownloadDetailReportCommandHandler : StatisticsReportCommandHandlerBase<PackageDownloadDetailReportCommand, DownloadStatisticsReport>
    {
        public PackageDownloadDetailReportCommandHandler(IFileStorageService storageService, IDiagnosticsService diagnosticsService)
            : base(storageService, diagnosticsService) { }
        
        protected override DownloadStatisticsReport ParseReport(IDiagnosticsSource trace, string reportContent)
        {
            DownloadStatisticsReport report = new DownloadStatisticsReport();
            report.Facts = CreateFacts(JObject.Parse(reportContent));
            return report;
        }

        private static IList<StatisticsFact> CreateFacts(JObject data)
        {
            IList<StatisticsFact> facts = new List<StatisticsFact>();

            foreach (JObject perVersion in data["Items"])
            {
                string version = (string)perVersion["Version"];

                foreach (JObject perClient in perVersion["Items"])
                {
                    string clientName = (string)perClient["ClientName"];
                    string clientVersion = (string)perClient["ClientVersion"];

                    string operation = "unknown";

                    JToken opt;
                    if (perClient.TryGetValue("Operation", out opt))
                    {
                        operation = (string)opt;
                    }

                    int downloads = (int)perClient["Downloads"];

                    facts.Add(new StatisticsFact(CreateDimensions(version, clientName, clientVersion, operation), downloads));
                }
            }

            return facts;
        }

        private static IDictionary<string, string> CreateDimensions(string version, string clientName, string clientVersion, string operation)
        {
            return new Dictionary<string, string> 
            { 
                { "Version", version },
                { "ClientName", clientName },
                { "ClientVersion", clientVersion },
                { "Operation", operation }
            };
        }
    }
}
