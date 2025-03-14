// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    // https://tools.ietf.org/html/rfc5280#section-5.3.1
    public enum RevocationReason
    {
        Unspecified = 0,
        KeyCompromise = 1,
        CaCompromise = 2,
        AffiliationChanged = 3,
        Superseded = 4,
        CessationOfOperation = 5,
        CertificateHold = 6,
        // 7 is unused
        RemoveFromCRL = 8,
        PrivilegeWithdrawn = 9,
        AaCompromise = 10
    }
}
