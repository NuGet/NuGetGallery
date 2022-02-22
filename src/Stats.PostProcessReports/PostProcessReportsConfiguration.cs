// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stats.PostProcessReports
{
    public class PostProcessReportsConfiguration
    {
        public string StorageAccount { get; set; }


        public string SourceContainerName { get; set; }
        public string SourcePath { get; set; }

        public string WorkContainerName { get; set; }
        public string WorkPath { get; set; }

        public string DestinationContainerName { get; set; }
        public string DestinationPath { get; set; }

        public string DetailedReportDirectoryName { get; set; }
        public int ReportWriteDegreeOfParallelism { get; set; } = 10;
    }
}
