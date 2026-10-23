// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Configures existing email delivery and links for per-artifact promotion results.
    /// </summary>
    public class PromotionEmailConfiguration : MessageServiceConfiguration
    {
        public ServiceBusConfiguration ServiceBus { get; set; }

        public string PackageUrlTemplate { get; set; }

        public string ManagePackagesUrl { get; set; }

        public string EmailSettingsUrl { get; set; }
    }
}
