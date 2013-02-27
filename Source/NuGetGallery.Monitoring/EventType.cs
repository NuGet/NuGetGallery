using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Monitoring
{
    public enum EventType
    {
        /// <summary>
        /// Represents a successful monitoring operation
        /// </summary>
        Success,

        /// <summary>
        /// Represents a failed monitoring operation
        /// </summary>
        Failure,

        /// <summary>
        /// Represents an unhealthy report - Indicates that the site may be dead, but the monitor is still checking.
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Represents an degraded monitoring operation (not Failed, but not good)
        /// </summary>
        Degraded,

        /// <summary>
        /// Represents a report of the quality of the service monitored
        /// </summary>
        QualityOfService,

        /// <summary>
        /// Represents a report of a failure on the monitor's side
        /// </summary>
        MonitorFailure
    }
}
