// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 3161 (https://datatracker.ietf.org/doc/html/rfc3161#section-2.4.2):

            PKIFailureInfo ::= BIT STRING {
               badAlg               (0),
                 -- unrecognized or unsupported Algorithm Identifier
               badRequest           (2),
                 -- transaction not permitted or supported
               badDataFormat        (5),
                 -- the data submitted has the wrong format
               timeNotAvailable    (14),
                 -- the TSA's time source is not available
               unacceptedPolicy    (15),
                 -- the requested TSA policy is not supported by the TSA
               unacceptedExtension (16),
                 -- the requested extension is not supported by the TSA
                addInfoNotAvailable (17)
                  -- the additional information requested could not be understood
                  -- or is not available
                systemFailure       (25)
                  -- the request cannot be handled due to system failure  }
    */
    internal enum PkiFailureInfo
    {
        BadAlg = 0,
        BadRequest = 2,
        BadDataFormat = 5,
        TimeNotAvailable = 14,
        UnacceptedPolicy = 15,
        UnacceptedExtension = 16,
        AddInfoNotAvailable = 17,
        SystemFailure = 25
    }
}
