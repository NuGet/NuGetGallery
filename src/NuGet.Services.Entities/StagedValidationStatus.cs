// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// The validation state of a package in <see cref="PackageStatus.Staged"/>. A staged package stays in
    /// <see cref="PackageStatus.Staged"/> for its whole time in staging; this tracks how far validation got.
    /// </summary>
    public enum StagedValidationStatus
    {
        /// <summary>
        /// Validation is in progress. This is the state a package is created in at push time.
        /// </summary>
        Validating = 0,

        /// <summary>
        /// Validation completed successfully. Only packages in this state may be promoted.
        /// </summary>
        Succeeded = 1,

        /// <summary>
        /// Validation failed. The package remains staged so the owner can inspect the failure, replace it, or
        /// delete it, but it can never be promoted.
        /// </summary>
        Failed = 2,
    }
}
