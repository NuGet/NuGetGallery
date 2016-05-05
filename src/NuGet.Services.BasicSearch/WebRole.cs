// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.BasicSearch
{
    public class WebRole
        : RoleEntryPoint
    {
        private string _localUrl;

        public override bool OnStart()
        {
            // Set local URL and ping self (make sure the app pool is warm before joining the load balancer)
            _localUrl = string.Format("http://{0}:{1}/",
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HttpEndpoint"].IPEndpoint.Address,
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["HttpEndpoint"].IPEndpoint.Port.ToString(CultureInfo.InvariantCulture));

            MakeRequest(_localUrl);

            return base.OnStart();
        }

        public override void Run()
        {
            while (true)
            {
                MakeRequest(_localUrl);

                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }

        private void MakeRequest(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "NuGet-Services-BasicSearch";
                using (var response = request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}