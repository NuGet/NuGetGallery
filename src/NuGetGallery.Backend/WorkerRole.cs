using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Backend.Worker
{
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            // 0. Set up ETL tracing
            // 1. Create a job dispatcher using Jobs imported from MEF
            // 2. Open the Queue
            // 3. Dispatch loop:
            //      a. Get next message (sleep until message available)
            //      b. Dispatch message
            //      c. Write ETL events from that invocation to blob
            //      d. Record results and events log to Status table
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }
    }
}
