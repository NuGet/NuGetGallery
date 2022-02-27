// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stats.PostProcessReports
{
    public interface ITelemetryService
    {
        /// <summary>
        /// Reports results of processing a single input file.
        /// </summary>
        void ReportFileProcessed(int fileLines, int filesCreated, int linesFailed);

        /// <summary>
        /// Reports results of processing all incoming file set.
        /// </summary>
        void ReportTotals(int totalInputFiles, int totalLines, int totalFilesCreated, int totalFailedLines);

        void ReportSourceAge(double sourceAgeHours);
    }
}
