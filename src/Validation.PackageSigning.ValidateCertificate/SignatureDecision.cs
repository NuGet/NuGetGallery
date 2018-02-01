// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// The ways an <see cref="EndCertificate"/>'s dependent <see cref="PackageSignature"/> may be affected by
    /// a change in the certificate's status.
    /// </summary>
    public enum SignatureDecision
    {
        /// <summary>
        /// The <see cref="PackageSignature"/> is unaffected by the status change of the <see cref="EndCertificate"/>.
        /// </summary>
        Ignore,

        /// <summary>
        /// The <see cref="PackageSignature"/> may be affected by the status change of the <see cref="EndCertificate"/>.
        /// The NuGet client may no longer accept this signature, however, the package does not necessarily need to be
        /// deleted from the server. This decision will only happen for packages whose status is currently
        /// <see cref="PackageSignatureStatus.Valid"/>.
        /// </summary>
        Warn,

        /// <summary>
        /// The <see cref="PackageSignature"/> is affected by the status change of the <see cref="EndCertificate"/>.
        /// The package should be deleted by an administrator.
        /// </summary>
        Reject,
    }
}
