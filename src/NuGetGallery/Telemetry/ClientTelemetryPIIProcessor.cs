// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

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
                requestTelemetryItem.Url = ObfuscateUri(requestTelemetryItem);
            }
        }

        private Uri ObfuscateUri(RequestTelemetry telemetryItem)
        {
            if(IsPIIOperation(telemetryItem.Context.Operation.Name))
            {
                // The new url form will be: https://nuget.org/ObfuscatedUserName
                return new Uri(Obfuscator.DefaultObfuscatedUrl(telemetryItem.Url));
            }
            return telemetryItem.Url;
        }

        protected virtual bool IsPIIOperation(string operationName)
        {
            if(string.IsNullOrEmpty(operationName))
            {
                return false;
            }
            // Remove the verb from the operation name.
            // An example of operationName : GET Users/Profiles
            return Obfuscator.ObfuscatedActions.Contains(operationName.Split(' ').Last());
        }
    }
}