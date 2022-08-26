// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGetGallery
{
    public class CustomerResourceIdEnricher : ITelemetryInitializer
    {
        private const string CustomerResourceId = "CustomerResourceId";
        private const string CustomerResourceIdConstant = "CustomerResourceIdConstant";
        private const string Prefix = "/tenants/";
        private static readonly string Empty = Prefix + Guid.Empty.ToString();

        private static readonly HashSet<string> CustomMetricNames = new HashSet<string>
        {
            TelemetryService.Events.PackagePush,
            TelemetryService.Events.PackagePushFailure,
            TelemetryService.Events.PackagePushDisconnect,
            TelemetryService.Events.SymbolPackagePush,
            TelemetryService.Events.SymbolPackagePushFailure,
            TelemetryService.Events.SymbolPackagePushDisconnect,
        };

        public void Initialize(ITelemetry telemetry)
        {
            var metric = telemetry as MetricTelemetry;
            if (metric == null)
            {
                return;
            }

            if (!CustomMetricNames.Contains(metric.Name))
            {
                return;
            }

            var itemTelemetry = telemetry as ISupportProperties;
            if (itemTelemetry == null)
            {
                return;
            }

            var httpContext = GetHttpContext();
            var tenantId = httpContext?.User?.Identity.GetTenantIdOrNull();
            var customerResourceId = tenantId != null ? Prefix + tenantId : Empty;
            itemTelemetry.Properties[CustomerResourceId] = customerResourceId;

            // This is necessary for the NuGet SLO/SLO pipeline to signal that there is a single "tenant" on NuGet.org
            // when measuring package push availability. This is an alernative approach to splitting package push
            // availability by authenticated user tenant (the "CustomerResourceId" property added above). The drawback
            // for using the authenticated user tenant is that it splits the availability measurement by tenant, leading
            // to very noisy results since the total volume per user tenant is quite low.
            //
            // Any constant value can be used here.
            itemTelemetry.Properties[CustomerResourceIdConstant] = nameof(NuGetGallery);
        }

        protected virtual HttpContextBase GetHttpContext() => HttpContextBaseExtensions.GetCurrent();
    }
}