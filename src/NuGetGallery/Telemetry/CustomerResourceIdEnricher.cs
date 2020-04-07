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
        private const string Prefix = "/tenants/";
        private static readonly string Empty = Prefix + Guid.Empty.ToString();

        private static readonly HashSet<string> CustomMetricNames = new HashSet<string>
        {
            TelemetryService.Events.PackagePush,
            TelemetryService.Events.PackagePushFailure,
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
        }

        protected virtual HttpContextBase GetHttpContext() => HttpContextBaseExtensions.GetCurrent();
    }
}