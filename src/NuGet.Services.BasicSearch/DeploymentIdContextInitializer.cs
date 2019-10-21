// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.BasicSearch
{
    public class DeploymentIdTelemetryInitializer : ITelemetryInitializer
    {
        public const string DeploymentId = "DeploymentId";

        public void Initialize(ITelemetry telemetry)
        {
            if (SafeRoleEnvironment.TryGetDeploymentId(out var id))
            {
                var supportProperties = (ISupportProperties)telemetry;
                supportProperties.Properties[DeploymentId] = id;
            }
        }
    }
}