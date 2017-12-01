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
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        internal const string DefaultTelemetryUserName = "HiddenUserName";

        internal static readonly HashSet<string> PiiActions = new HashSet<string>{
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/CancelPendingOwnershipRequest",
            "Users/Confirm",
            "Users/Delete",
            "Users/Profiles",
            "Users/ResetPassword"};

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
                // The new url form will be: https://nuget.org/HiddenUserName
                return new Uri($"{telemetryItem.Url.Scheme}://{telemetryItem.Url.Host}/{DefaultTelemetryUserName}");
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
            return PiiActions.Contains(operationName.Split(' ').Last());
        }
    }
}