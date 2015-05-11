// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class StatisticsReport
    {
        public string Content { get; private set; }
        public DateTime? LastUpdatedUtc { get; private set; }

        public StatisticsReport(string content, DateTime? lastUpdatedUtc)
        {
            Content = content;
            LastUpdatedUtc = lastUpdatedUtc;
        }
    }
}
