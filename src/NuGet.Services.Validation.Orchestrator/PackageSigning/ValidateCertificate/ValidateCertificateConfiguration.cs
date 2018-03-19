// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.Validation.PackageSigning.ValidateCertificate
{
    public class ValidateCertificateConfiguration
    {
        /// <summary>
        /// How stale certificates' statuses can be before revalidating.
        /// </summary>
        public TimeSpan? CertificateRevalidationThreshold { get; set; }

        /// <summary>
        /// The visibility delay to apply to Service Bus messages requesting a new validation.
        /// </summary>
        public TimeSpan? MessageDelay { get; set; }

        /// <summary>
        /// The Service Bus configuration used to enqueue package certificate validations.
        /// </summary>
        public ServiceBusConfiguration ServiceBus { get; set; }
    }
}