// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.Orchestrator.ContentScan
{
    public class ContentScanConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue content scan validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }
    }
}
