// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Logging;

namespace Stats.PostProcessReports
{
    public class TelemetryService : ITelemetryService
    {
        private const string FileLinesMetricName = "FileLines";
        private const string FilesCreatedMetricName = "FilesCreated";
        private const string LinesFailedMetricName = "LinesFailed";
        private const string TotalInputFilesMetricName = "TotalInputFilesProcessed";
        private const string TotalLinesProcessedMetricName = "TotalLinesProcessed";
        private const string TotalFilesCreatedMetricName = "TotalFilesCreated";
        private const string TotalLinesFailedMetricName = "TotalLinesFailed";
        private const string SourceReportAgeHours = "SourceReportAgeHours";

        private readonly ITelemetryClient _telemetryClient;

        public TelemetryService(
            ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        public void ReportFileProcessed(int fileLines, int filesCreated, int linesFailed)
        {
            _telemetryClient.TrackMetric(FileLinesMetricName, fileLines);
            _telemetryClient.TrackMetric(FilesCreatedMetricName, filesCreated);
            _telemetryClient.TrackMetric(LinesFailedMetricName, linesFailed);
        }

        public void ReportTotals(int totalInputFiles, int totalLines, int totalFiles, int totalFailedLines)
        {
            _telemetryClient.TrackMetric(TotalInputFilesMetricName, totalInputFiles);
            _telemetryClient.TrackMetric(TotalLinesProcessedMetricName, totalLines);
            _telemetryClient.TrackMetric(TotalFilesCreatedMetricName, totalFiles);
            _telemetryClient.TrackMetric(TotalLinesFailedMetricName, totalFailedLines);
        }

        public void ReportSourceAge(double sourceAgeHours)
        {
            _telemetryClient.TrackMetric(SourceReportAgeHours, sourceAgeHours);
        }
    }
}
