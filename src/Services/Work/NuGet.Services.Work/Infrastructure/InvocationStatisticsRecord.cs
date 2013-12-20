using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work
{
    /// <summary>
    /// Represents a single row out of the InvocationStatistics, JobStatistics or InstanceStatistics views
    /// </summary>
    public class InvocationStatisticsRecord
    {
        /// <summary>
        /// Gets the item this row relates to (null in the case of InvocationStatistics, which returns only a single row)
        /// </summary>
        public string Item { get; set; }

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
    }
}
