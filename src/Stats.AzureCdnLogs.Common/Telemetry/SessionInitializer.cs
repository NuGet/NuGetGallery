// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Stats.AzureCdnLogs.Common
{
    internal class SessionInitializer
        : IContextInitializer
    {
        public void Initialize(TelemetryContext context)
        {
            context.User.Id = Environment.UserName;
            context.Session.Id = Guid.NewGuid().ToString("D");
            context.Device.Id = Environment.MachineName;
            context.Device.RoleInstance = Environment.MachineName;
            context.Device.OperatingSystem = Environment.OSVersion.ToString();
            context.Component.Version = typeof(SessionInitializer).Assembly.GetName().Version.ToString();
            context.Device.RoleName = Assembly.GetEntryAssembly().GetName().Name;
        }
}