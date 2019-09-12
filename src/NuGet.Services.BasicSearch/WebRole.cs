// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.BasicSearch
{
    public class WebRole
        : RoleEntryPoint
    {
        private HttpClient _httpClient;
        private string _localUrl;

        public override bool OnStart()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "NuGet-Services-BasicSearch");

            // Set local URL and ping self (make sure the app pool is warm before joining the load balancer)
            _localUrl = string.Format("http://{0}:{1}/",
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HttpEndpoint"].IPEndpoint.Address,
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HttpEndpoint"].IPEndpoint.Port.ToString(CultureInfo.InvariantCulture));

            MakeRequestAsync(_httpClient, _localUrl).GetAwaiter().GetResult();

            return base.OnStart();
        }

        public override void Run()
        {
            while (true)
            {
                MakeRequestAsync(_httpClient, _localUrl).GetAwaiter().GetResult();

                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }

        public override void OnStop()
        {
            // Once the Stopping event is raised, the load balancer stops sending requests to the WebRole.
            // We'll attempt to gracefully handle any pending requests to this instance before stopping the role.
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

            _httpClient = null;
        }

        private async Task MakeRequestAsync(HttpClient httpClient, string url)
        {
            if (httpClient == null)
            {
                return;
            }

            try
            {
                using (var response = await httpClient.GetAsync(url))
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var contentReader = new StreamReader(contentStream))
                {
                    contentReader.ReadToEnd();
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}