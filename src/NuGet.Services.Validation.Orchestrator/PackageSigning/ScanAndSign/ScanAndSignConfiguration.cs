// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public class ScanAndSignConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue package signing validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }

        /// <summary>
        /// The criteria used to determine if a package should be submitted scanning.
        /// </summary>
        public PackageCriteria PackageCriteria { get; set; } = new PackageCriteria();

        /// <summary>
        /// If true, packages with no repository signatures will be repository signed.
        /// </summary>
        public bool RepositorySigningEnabled { get; set; }

        /// <summary>
        /// The service index URL that should be stamped on repository signatures.
        /// </summary>
        public string V3ServiceIndexUrl { get; set; }
    }
}
