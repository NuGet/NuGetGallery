using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Api.Models
{
    public class InvocationStatisticsModel
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

        public InvocationStatisticsModel() { }
        public InvocationStatisticsModel(InvocationStatistics stats)
        {
            Queued = stats.Queued;
            Dequeued = stats.Dequeued;
            Executing = stats.Executing;
            Executed = stats.Executed;
            Cancelled = stats.Cancelled;
            Suspended = stats.Suspended;
            Completed = stats.Completed;
            Faulted = stats.Faulted;
            Crashed = stats.Crashed;
            Aborted = stats.Aborted;
            Total = stats.Total;
        }
    }

    public class JobStatisticsModel : InvocationStatisticsModel
    {
        public string Job { get; set; }

        public JobStatisticsModel() : base() { }
        public JobStatisticsModel(InvocationStatistics stats)
            : base(stats)
        {
            Job = stats.Item;
        }
    }

    public class InstanceStatisticsModel : InvocationStatisticsModel
    {
        public string Instance { get; set; }

        public InstanceStatisticsModel() : base() { }
        public InstanceStatisticsModel(InvocationStatistics stats)
            : base(stats)
        {
            Instance = stats.Item;
        }
    }
}
