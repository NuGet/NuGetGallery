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
        /// The <see cref="Certificate" /> is valid and has not been revoked.
        /// </summary>
        Good = 1,

        /// <summary>
        /// The <see cref="Certificate" /> has failed offline validations. This could be for a number of reasons including
        /// an untrusted root or weak hashing algorithm. Anything signed by the certificate should be considered invalid.
        /// </summary>
        Invalid = 2,

        /// <summary>
        /// The <see cref="Certificate"/> has been revoked by the certificate authority. Anything signed by the certificate
        /// after <see cref="Certificate.RevocationTime"/> should be considered invalid.
        /// </summary>
        Revoked = 3,
    }
}
