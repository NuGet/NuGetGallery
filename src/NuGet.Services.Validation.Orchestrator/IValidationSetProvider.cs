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
    public interface IValidationSetProvider<T> where T : class, IEntity
    {
        /// <summary>
        /// Reads validation set data from storage, creates one if did not exist in storage
        /// </summary>
        /// <param name="message">The validation message data. It contains the validation tracking id</param>
        /// <param name="validatingEntity">The validating entity</param>
        /// <returns><see cref="PackageValidationSet"/> object with information about
        /// requested <paramref name="validationTrackingId"/>. Null if no further processing
        /// should be made (e.g. duplicate validation request was detected).
        /// </returns>
        Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(PackageValidationMessageData message, IValidatingEntity<T> validatingEntity);
    }
}
