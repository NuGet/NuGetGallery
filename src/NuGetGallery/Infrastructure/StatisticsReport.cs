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
