// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// Represents the status of a <see cref="PackageSigningState"/>'s signing.
    /// </summary>
    public enum PackageSigningStatus
    {
        /// <summary>
        /// One or more of the package's signature is invalid.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The package has no signatures.
        /// </summary>
        Unsigned = 1,

        /// <summary>
        /// All of the package's signatures are valid or in their grace periods.
        /// </summary>
        Valid = 2,
    }
}
