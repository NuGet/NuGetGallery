// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2):

            PKIStatus ::= INTEGER {
                granted                (0),
                -- when the PKIStatus contains the value zero a TimeStampToken, as
                    requested, is present.
                grantedWithMods        (1),
                -- when the PKIStatus contains the value one a TimeStampToken,
                    with modifications, is present.
                rejection              (2),
                waiting                (3),
                revocationWarning      (4),
                -- this message contains a warning that a revocation is
                -- imminent
                revocationNotification (5)
                -- notification that a revocation has occurred  }
    */
    internal enum PkiStatus
    {
        Granted = 0,
        GrantedWithMods = 1,
        Rejection = 2,
        Waiting = 3,
        RevocationWarning = 4,
        RevocationNotification = 5
    }
}
