using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Monitoring
{
    /// <summary>
    /// Thread-safe interface to reporting monitoring status
    /// </summary>
    public interface IEventReporter
    {
        void Report(MonitoringEvent evt);
    }
}
