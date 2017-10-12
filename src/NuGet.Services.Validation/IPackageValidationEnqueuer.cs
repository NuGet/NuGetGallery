// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    public interface IPackageValidationEnqueuer
    {
        /// <summary>
        /// Enqueues validation of the specified package to start ASAP.
        /// </summary>
        /// <param name="message">Package information</param>
        Task StartValidationAsync(PackageValidationMessageData message);
        
        /// <summary>
        /// Enqueues validation of the specified package to start no sooner than specified time
        /// </summary>
        /// <param name="message">Package information</param>
        /// <param name="postponeProcessingTill">The time till which validation processing should be postponed.</param>
        Task StartValidationAsync(PackageValidationMessageData message, DateTimeOffset postponeProcessingTill);
    }
}
