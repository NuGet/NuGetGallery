// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.PostProcessReports
{
    public class TotalStats
    {
        public int SourceFilesProcessed { get; set; }
        public int TotalLinesProcessed { get; set; }
        public int TotalLinesFailed { get; set; }
        public int TotalFilesCreated { get; set; }
    }
}
