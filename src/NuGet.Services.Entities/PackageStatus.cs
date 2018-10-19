// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    public enum PackageStatus
    {
        /// <summary>
        /// The package is visible and available for all normal package operations.
        /// </summary>
        Available = 0,

        /// <summary>
        /// The package has been soft deleted. This means that the package is not available but the package ID and
        /// version are still reserved.
        /// </summary>
        Deleted = 1,

        /// <summary>
        /// The package is in the process of being validated. The validation is being done asynchronously. When the
        /// validation is complete it will move to the <see cref="Available"/>, <see cref="Deleted"/>, or
        /// <see cref="FailedValidation"/> state. When packages are in this state, they are not available but the
        /// package ID and version are still reserved.
        /// </summary>
        Validating = 2,

        /// <summary>
        /// The package has completed validation and the result has come back as a failure. When packages are in this
        /// state, they are not available but the package ID and version are still reserved.
        /// </summary>
        FailedValidation = 3,
    }
}
