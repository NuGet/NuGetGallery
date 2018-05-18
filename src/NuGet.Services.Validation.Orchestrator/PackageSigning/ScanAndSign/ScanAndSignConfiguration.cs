// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Jobs.Configuration;
using NuGet.Services.Validation.Vcs;

namespace NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign
{
    public class ScanAndSignConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue package signing validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }

        /// <summary>
        /// The visibility delay to apply to Service Bus messages requesting a new validation.
        /// </summary>
        public TimeSpan? MessageDelay { get; set; }

        /// <summary>
        /// The criteria used to determine if a package should be submitted scanning.
        /// </summary>
        public PackageCriteria PackageCriteria { get; set; } = new PackageCriteria();
    }
}
