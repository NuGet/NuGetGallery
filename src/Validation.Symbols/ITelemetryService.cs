// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;

namespace Validation.Symbols
{
    public interface ITelemetryService : ISubscriptionProcessorTelemetryService
    {
        /// <summary>
        /// Tracks the metric for the packages not being found.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        void TrackPackageNotFoundEvent(string packageId, string packageNormalizedVersion);

        /// <summary>
        /// Tracks the metric for the symbol packages not being found.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        void TrackSymbolsPackageNotFoundEvent(string packageId, string packageNormalizedVersion);

        /// <summary>
        /// Tracks the metric for the validation execution time.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="symbolCount">The count of symbols validated.</param>
        IDisposable TrackSymbolValidationDurationEvent(string packageId, string packageNormalizedVersion, int symbolCount);

        /// <summary>
        /// Tracks the status of the validation per assembly.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="validationStatus">The validation result.</param>
        /// <param name="issue">Information about the issue id failed or empty if passed..</param>
        /// <param name="assemblyName">The assembly name.</param>
        void TrackSymbolsAssemblyValidationResultEvent(string packageId, string packageNormalizedVersion, ValidationStatus validationStatus, string issue, string assemblyName);

        /// <summary>
        /// Tracks the status of the validation per package.
        /// </summary>
        /// <param name="packageId">The package id.</param>
        /// <param name="packageNormalizedVersion">The package normalized version.</param>
        /// <param name="validationStatus">The validation result.</param>
        void TrackSymbolsValidationResultEvent(string packageId, string packageNormalizedVersion, ValidationStatus validationStatus);
    }
}
