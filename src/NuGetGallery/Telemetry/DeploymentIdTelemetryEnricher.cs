// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery
{
    public class DeploymentIdTelemetryEnricher : ITelemetryInitializer
    {
        private const string PropertyKey = "CloudDeploymentId";

        private static readonly Lazy<string> LazyDeploymentId = new Lazy<string>(() =>
        {
            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    return RoleEnvironment.DeploymentId;
                }
            }
            catch
            {
                // This likely means the cloud service runtime is not available.
            }

            return null;
        });

        internal virtual string DeploymentId => LazyDeploymentId.Value;

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null
                || DeploymentId == null)
            {
                return;
            }

            var itemTelemetry = telemetry as ISupportProperties;
            if (itemTelemetry == null)
            {
                return;
            }

            if (itemTelemetry.Properties.ContainsKey(PropertyKey))
            {
                return;
            }

            itemTelemetry.Properties.Add(PropertyKey, DeploymentId);
        }
    }
}