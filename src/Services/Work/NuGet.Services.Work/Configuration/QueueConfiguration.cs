using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace NuGet.Services.Work.Configuration
{
    public class QueueConfiguration
    {
        [Description("The amount of time the job service will sleep when it detects no more work is available")]
        public TimeSpan PollInterval { get; set; }
    }
}
