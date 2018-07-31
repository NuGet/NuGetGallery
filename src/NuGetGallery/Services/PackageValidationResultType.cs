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

        /// <summary>
        /// The package is invalid because it should not be signed given the current required signer certificates (or
        /// lack thereof).
        /// </summary>
        PackageShouldNotBeSigned,

        /// <summary>
        /// Similar to <see cref="PackageShouldNotBeSigned"/> but the current user can manage certificates and
        /// potentially remediate the situation.
        /// </summary>
        PackageShouldNotBeSignedButCanManageCertificates,
    }
}