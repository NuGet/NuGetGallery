// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The type of <see cref="PackageValidationResult"/>.
    /// </summary>
    public enum PackageValidationResultType
    {
        /// <summary>
        /// The package is valid based on the performed validations. Note that the caller may perform other validations
        /// so this is not an all inclusive validation.
        /// </summary>
        Accepted,

        /// <summary>
        /// The package is invalid based on the package content.
        /// </summary>
        Invalid,
    }
}