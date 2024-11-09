// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           OCSPResponse ::= SEQUENCE {
              responseStatus         OCSPResponseStatus,
              responseBytes          [0] EXPLICIT ResponseBytes OPTIONAL }
    */
    internal sealed class OcspResponse
    {
        internal OcspResponseStatus ResponseStatus { get; }
        internal ResponseBytes? ResponseBytes { get; }

        internal OcspResponse(OcspResponseStatus responseStatus, ResponseBytes? responseBytes)
        {
            ResponseStatus = responseStatus;
            ResponseBytes = responseBytes;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteEnumeratedValue(ResponseStatus);

                if (ResponseBytes is not null)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                    {
                        ResponseBytes.Encode(writer);
                    }
                }
            }
        }
    }
}
