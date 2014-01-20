using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Work
{
    public class JobContinuation
    {
        public TimeSpan WaitPeriod { get; private set; }
        public Dictionary<string, string> Parameters { get; private set; }

        public JobContinuation(TimeSpan waitPeriod, Dictionary<string, string> parameters)
        {
            WaitPeriod = waitPeriod;
            Parameters = parameters;
        }
    }
}
