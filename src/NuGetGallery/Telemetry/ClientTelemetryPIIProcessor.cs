// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace NuGetGallery
{
    public class ClientTelemetryPIIProcessor : ITelemetryProcessor
    {
        private ITelemetryProcessor Next { get; }

        public ClientTelemetryPIIProcessor(ITelemetryProcessor next)
        {
            this.Next = next;
        }

        public void Process(ITelemetry item)
        {
            ModifyItem(item);
            this.Next.Process(item);
        }

        private void ModifyItem(ITelemetry item)
        {
            var requestTelemetryItem = item as RequestTelemetry;
            if(requestTelemetryItem != null)
            {
                requestTelemetryItem.Url = Obfuscator.ObfuscateUrl(requestTelemetryItem.Url);
                requestTelemetryItem.Name = Obfuscator.ObfuscateName(requestTelemetryItem.Name);
                if(requestTelemetryItem.Context.Operation != null)
                {
                    requestTelemetryItem.Context.Operation.Name = Obfuscator.ObfuscateName(requestTelemetryItem.Context.Operation.Name);
                }
            }
        }
    }
}