using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Exception thrown when the stats report is not found.
    /// </summary>
    [Serializable]
    public class StatisticsReportNotFoundException : Exception
    {
        public StatisticsReportNotFoundException() { }
        public StatisticsReportNotFoundException(string message) : base(message) { }
        public StatisticsReportNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected StatisticsReportNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}