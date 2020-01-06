// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Services.Logging
{
    public class TelemetryContextInitializer
        : ITelemetryInitializer
    {
        private readonly string _deviceId;
        private readonly string _deviceOS;
        private readonly string _cloudRoleName;
        private readonly string _componentVersion;

        public TelemetryContextInitializer()
        {
            try
            {
                _deviceId = Environment.MachineName;

                if (Environment.OSVersion != null)
                {
                    _deviceOS = Environment.OSVersion.ToString();
                }

                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var assemblyName = entryAssembly.GetName();

                    _cloudRoleName = assemblyName.Name;

                    if (assemblyName.Version != null)
                    {
                        _componentVersion = assemblyName.Version.ToString();
                    }
                }
            }
            catch
            {
                // Guess we won't have this additional metadata in our logs then...
                // Prefer to have logs without this metadata rather than not having none :)
            }
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry?.Context == null)
            {
                return;
            }

            if (_deviceId != null)
            {
                telemetry.Context.Device.Id = _deviceId;
                telemetry.Context.Cloud.RoleInstance = _deviceId;
            }

            if (_deviceOS != null)
            {
                telemetry.Context.Device.OperatingSystem = _deviceOS;
            }

            if (_cloudRoleName != null)
            {
                telemetry.Context.Cloud.RoleName = _cloudRoleName;
            }

            if (_componentVersion != null)
            {
                telemetry.Context.Component.Version = _componentVersion;
            }
        }
    }
}
