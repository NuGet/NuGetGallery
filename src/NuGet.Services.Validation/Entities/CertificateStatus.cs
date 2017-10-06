// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status for a given <see cref="Certificate"/>.
    /// </summary>
    public enum CertificateStatus
    {
        /// <summary>
        /// The status is unknown if this <see cref="Certificate"/>'s online verification has never completed.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The <see cref="Certificate" /> has not been revoked.
        /// </summary>
        Good = 1,

        /// <summary>
        /// The <see cref="Certificate" /> has been revoked.
        /// </summary>
        Revoked = 2,
    }
}
