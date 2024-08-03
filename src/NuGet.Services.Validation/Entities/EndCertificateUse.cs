// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// How an end certificate is being used.
    /// </summary>
    public enum EndCertificateUse
    {
        /// <summary>
        /// An end certificate used in a <see cref="PackageSignature"/>.
        /// </summary>
        CodeSigning = 1,

        /// <summary>
        /// An end certificate used in a <see cref="TrustedTimestamp"/>.
        /// </summary>
        Timestamping = 2,
    }
}
