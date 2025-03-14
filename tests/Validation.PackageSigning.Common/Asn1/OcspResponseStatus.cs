// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           OCSPResponseStatus ::= ENUMERATED {
               successful            (0),  -- Response has valid confirmations
               malformedRequest      (1),  -- Illegal confirmation request
               internalError         (2),  -- Internal error in issuer
               tryLater              (3),  -- Try again later
                                           -- (4) is not used
               sigRequired           (5),  -- Must sign the request
               unauthorized          (6)   -- Request unauthorized
           }
    */
    internal enum OcspResponseStatus
    {
        Successful = 0,
        MalformedRequest = 1,
        InternalError = 2,
        TryLater = 3,
        SigRequired = 5,
        Unauthorized = 6
    }
}
