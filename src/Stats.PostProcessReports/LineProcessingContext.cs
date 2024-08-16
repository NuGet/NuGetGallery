// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.PostProcessReports
{
    /// <summary>
    /// Contains information needed to process a single line of a incoming report blob,
    /// log the details and calculate job statistics.
    /// </summary>
    public class LineProcessingContext
    {
        public int LineNumber { get; set; }
        public string Data { get; set; }
    };
}
