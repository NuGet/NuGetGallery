// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.SearchService
{
    /// <summary>
    /// Overrides the initialized telemetry context. This should be added last in
    /// the Application Insights telemetry list.
    /// See: https://github.com/microsoft/ApplicationInsights-dotnet-server/blob/e5a0edbe570e0938d3cb7a36a57b25d0db4d3c01/Src/WindowsServer/WindowsServer.Shared/AzureWebAppRoleEnvironmentTelemetryInitializer.cs#L12
    /// </summary>
    public class AzureWebAppTelemetryInitializer : ITelemetryInitializer
    {
        private const string StagingSlotSuffix = "-staging";

        public void Initialize(ITelemetry telemetry)
        {
            // Application Insight's Azure Web App Role Environment telemetry initializer uses
            // the hostname for the "cloud_roleName" property, which unintentionally creates separate
            // role names for our production/staging slots.
            var roleName = telemetry.Context.Cloud.RoleName;
            if (!string.IsNullOrEmpty(roleName) && roleName.EndsWith(StagingSlotSuffix, StringComparison.OrdinalIgnoreCase))
            {
                telemetry.Context.Cloud.RoleName = roleName.Substring(0, roleName.Length - StagingSlotSuffix.Length);
            }
        }
    }
}