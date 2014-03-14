using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsGlimpseTab : TabBase
    {
        public override object GetData(ITabContext context)
        {
            return new
            {
                MessageQueues = MessageQueue.GetQueueStats()
            };
        }

        public override string Name
        {
            get { return "Diag"; }
        }
    }
}