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
    public abstract class StatisticsReportCommandBase<TReportType> : Command<Task<TReportType>>
    {
        public string ReportName { get; private set; }

        protected StatisticsReportCommandBase(string reportName)
        {
            ReportName = reportName;
        }

        public override bool Equals(object obj)
        {
            var other = obj as StatisticsReportCommandBase<TReportType>;
            return other != null &&
                String.Equals(ReportName, other.ReportName);
        }

        public override int GetHashCode()
        {
            return ReportName.GetHashCode();
        }
    }

    public abstract class StatisticsReportCommandHandlerBase<TCommand, TReportType> : CommandHandler<TCommand, Task<TReportType>> 
        where TCommand : StatisticsReportCommandBase<TReportType>
    {
        public IFileStorageService Storage { get; protected set; }
        public IDiagnosticsService Diagnostics { get; protected set; }

        public StatisticsReportCommandHandlerBase(IFileStorageService storageService, IDiagnosticsService diagnosticsService)
        {
            Storage = storageService;
            Diagnostics = diagnosticsService;
        }

        public override async Task<TReportType> Execute(TCommand cmd)
        {
            var trace = Diagnostics == null ? new NullDiagnosticsSource() : Diagnostics.GetSource("PackageDownloadsReportQuery");

            // Load the report from file storage
            string reportContent;
            var stream = await Storage.GetFileAsync("stats", "popularity/" + cmd.ReportName.ToLowerInvariant() + ".json");
            if (stream == null)
            {
                return default(TReportType);
            }

            // The reader will close the stream.
            using (var reader = new StreamReader(stream))
            {
                reportContent = await reader.ReadToEndAsync();
            }

            return ParseReport(trace, reportContent);
        }

        protected abstract TReportType ParseReport(IDiagnosticsSource trace, string reportContent);
    }
}