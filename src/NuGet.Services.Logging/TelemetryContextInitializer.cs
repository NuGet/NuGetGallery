// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    internal class TelemetryContextInitializer
        : ITelemetryInitializer
    {
        private readonly string _deviceId;
        private readonly string _deviceOS;
        private readonly string _cloudRoleName;
        private readonly string _componentVersion;

        public TelemetryContextInitializer()
        {
            _deviceId = Environment.MachineName;
            _deviceOS = Environment.OSVersion.ToString();

            var assemblyName = Assembly.GetEntryAssembly().GetName();
            _cloudRoleName = assemblyName.Name;
            _componentVersion = assemblyName.Version.ToString();
        }

        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Device.Id = _deviceId;
            telemetry.Context.Device.OperatingSystem = _deviceOS;
            telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
            telemetry.Context.Cloud.RoleName = _cloudRoleName;
            telemetry.Context.Component.Version = _componentVersion;
        }
    }
}