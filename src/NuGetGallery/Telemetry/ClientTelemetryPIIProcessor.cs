// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Owin;

namespace NuGetGallery
{
    public class ClientTelemetryPIIProcessor : ITelemetryProcessor
    {
        /// <summary>
        /// Default user name that will replace the real user name.
        /// This value will be saved in AI instead of the real value.
        /// </summary>
        public const string DefaultTelemetryUserName = "HiddenUserName";

        public static readonly HashSet<string> PiiActions = new HashSet<string>{
            "Packages/ConfirmPendingOwnershipRequest",
            "Packages/RejectPendingOwnershipRequest",
            "Packages/CancelPendingOwnershipRequest",
            "Users/Confirm",
            "Users/Delete",
            "Users/Profiles",
            "Users/ResetPassword"};

        private ITelemetryProcessor Next { get; set; }

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
            if(item != null && item is RequestTelemetry)
            {
                ((RequestTelemetry)item).Url = GetUri((RequestTelemetry)item);
            }
        }

        private Uri GetUri(RequestTelemetry telemetryItem)
        {
            if(IsPIIOperation(telemetryItem.Context.Operation.Name))
            {
                return new Uri($"{telemetryItem.Url.Scheme}://{telemetryItem.Url.Host}/{DefaultTelemetryUserName}");
            }
            return telemetryItem.Url;
        }

        protected virtual bool IsPIIOperation(string operationName)
        {
            // Remove the verb from the operation name.
            return PiiActions.Contains(operationName.Split(' ').Last());
        }
    }
}