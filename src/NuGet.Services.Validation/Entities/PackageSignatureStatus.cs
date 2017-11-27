// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The status of a <see cref="PackageSignature"/>.
    /// </summary>
    public enum PackageSignatureStatus
    {
        /// <summary>
        /// The signature is invalid. This may happen for a number of reasons, including
        /// untrusted certificates, revoked certificates, or mismatched signature metadata.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The signature's final state is unknown as it is still undergoing initial push validations. The packages
        /// should NOT be available for download yet!
        /// </summary>
        Unknown = 1,

        /// <summary>
        /// A signature is considered to be in its "grace period" if one of its certificate's status is unknown OR
        /// its last known status is older than the signature itself. In other words, a signature is in
        /// its grace period if one or more the signature's <see cref="EndCertificate"/>s' "StatusUpdateTime" is before
        /// <see cref="PackageSignature"/>'s "SignedAt".
        /// </summary>
        InGracePeriod = 2,

        /// <summary>
        /// The signature is valid.
        /// </summary>
        Valid = 3,
    }
}
