using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace NuGet.Services.Work.Configuration
{
    public class WorkConfiguration
    {
        [Description("The amount of time the job service will sleep when it detects no more work is available")]
        public TimeSpan PollInterval { get; set; }

        [Description("The number of workers to run per CPU core")]
        public int? WorkersPerCore { get; set; }

        [Description("The maximum number of workers to run")]
        public int? MaxWorkers{ get; set; }
    }
}
