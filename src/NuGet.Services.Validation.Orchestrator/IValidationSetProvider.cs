// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides <see cref="PackageValidationSet"/> instances.
    /// </summary>
    public interface IValidationSetProvider
    {
        /// <summary>
        /// Reads validation set data from storage, creates one if did not exist in storage
        /// </summary>
        /// <param name="validationTrackingId">Validation tracking id</param>
        /// <param name="package">Package details from Gallery DB</param>
        /// <returns><see cref="PackageValidationSet"/> object with information about requested <paramref name="validationTrackingId"/></returns>
        Task<PackageValidationSet> GetOrCreateValidationSetAsync(Guid validationTrackingId, Package package);
    }
}
