// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status for a given <see cref="EndCertificate"/>.
    /// </summary>
    public enum TrustedTimestampStatus
    {
        /// <summary>
        /// The <see cref="TrustedTimestamp" /> has failed validation. This could be for a number of reasons including
        /// an invalid or revoked certificate. The timestamp should no longer be trusted.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The <see cref="TrustedTimestamp" /> is valid and trusted.
        /// </summary>
        /// <remarks>
        /// Trusted timestamps may have this status before their certificates have been checked for revocation online.
        /// </remarks>
        Valid = 1,
    }
}
