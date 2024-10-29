// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class WebRole
        : RoleEntryPoint
    {
        public override void OnStop()
        {
            // Once the Stopping event is raised, the load balancer stops sending requests to the WebRole.
            // We'll attempt to gracefully handle any pending requests (e.g. package uploads) to this instance before stopping the role.
            // There's only a 5 mins window before the role will restart either way.
            // The Trace data from the OnStop method will never appear in WADLogsTable and is only accessible using DebugView (unless a tricky On-Demand Transfer is performed).
            // See also: https://azure.microsoft.com/en-us/blog/the-right-way-to-handle-azure-onstop-events/
            Trace.TraceInformation("OnStop called in WebRole");

            using (var currentRequestsPerfCounter = new PerformanceCounter(
                "ASP.NET",
                "Requests Current",
                instanceName: string.Empty,
                readOnly: true))
            {
                while (true)
                {
                    var value = currentRequestsPerfCounter.NextValue();
                    Trace.TraceInformation($"ASP.NET Requests Current = {value}");

                    if (value <= 0)
                    {
                        // No more pending requests: the role can be safely stopped.
                        break;
                    }

                    // Check again in a second.
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}