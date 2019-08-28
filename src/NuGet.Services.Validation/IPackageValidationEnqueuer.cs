// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.Validation
{
    public interface IPackageValidationEnqueuer
    {
        /// <summary>
        /// Enqueues a package validation message to be consumed as soon as possible.
        /// </summary>
        /// <param name="message">Package validation information</param>
        Task SendMessageAsync(PackageValidationMessageData message);

        /// <summary>
        /// Enqueues a package validation message to be consumed no sooner than the specified time.
        /// </summary>
        /// <param name="message">Package validation information</param>
        /// <param name="postponeProcessingTill">The time that validation processing should be postponed.</param>
        Task SendMessageAsync(PackageValidationMessageData message, DateTimeOffset postponeProcessingTill);
    }
}
