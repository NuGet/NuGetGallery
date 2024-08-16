// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// Configuration for initializing the <see cref="ProcessSignatureEnqueuer"/>.
    /// </summary>
    public class ProcessSignatureConfiguration
    {
        /// <summary>
        /// The Service Bus configuration used to enqueue package signing validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }

        /// <summary>
        /// The visibility delay to apply to Service Bus messages requesting a new validation.
        /// </summary>
        public TimeSpan? MessageDelay { get; set; }
    }
}
