using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    public class CreateWarehouseReportsJob : JobHandler<CreateWarehouseReportsEventSource>
    {
        protected internal override Task Execute()
        {
            
        }
    }

    [EventSource("Outercurve-NuGet-Jobs-CreateWarehouseReports")]
    public class CreateWarehouseReportsEventSource : EventSource
    {
        public static readonly CreateWarehouseReportsEventSource Log = new CreateWarehouseReportsEventSource();
        private CreateWarehouseReportsEventSource() { }

        public static class Tasks
        {
            public const EventTask GeneratingReports = (EventTask)0x1;
        }
    }
}
