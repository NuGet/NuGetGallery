using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Models
{
    public class InvocationStatistics
    {
        public int Queued { get; set; }
        public int Dequeued { get; set; }
        public int Executing { get; set; }
        public int Executed { get; set; }
        public int Cancelled { get; set; }
        public int Suspended { get; set; }
        public int Completed { get; set; }
        public int Faulted { get; set; }
        public int Crashed { get; set; }
        public int Aborted { get; set; }
        public int Total { get; set; }

        public InvocationStatistics() { }
    }

    public class JobStatistics : InvocationStatistics
    {
        public string Job { get; set; }

        public JobStatistics() : base() { }
    }

    public class InstanceStatistics : InvocationStatistics
    {
        public string Instance { get; set; }

        public InstanceStatistics() : base() { }
    }
}
