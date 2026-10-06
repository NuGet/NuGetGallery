// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace Gallery.Maintenance
{
    /// <summary>
    /// Configures existing email delivery and owner actions for staging expiration.
    /// </summary>
    public class MaintenanceEmailConfiguration : MessageServiceConfiguration
    {
        public ServiceBusConfiguration ServiceBus { get; set; }

        public string ManagePackagesUrl { get; set; }

        public string EmailSettingsUrl { get; set; }
    }
}
