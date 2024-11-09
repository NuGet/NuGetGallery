// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           The value for responseBytes consists of an OBJECT IDENTIFIER and a
           response syntax identified by that OID encoded as an OCTET STRING.

           ResponseBytes ::=       SEQUENCE {
               responseType   OBJECT IDENTIFIER,
               response       OCTET STRING }

           For a basic OCSP responder, responseType will be id-pkix-ocsp-basic.

           id-pkix-ocsp           OBJECT IDENTIFIER ::= { id-ad-ocsp }
           id-pkix-ocsp-basic     OBJECT IDENTIFIER ::= { id-pkix-ocsp 1 }

           OCSP responders SHALL be capable of producing responses of the
           id-pkix-ocsp-basic response type.  Correspondingly, OCSP clients
           SHALL be capable of receiving and processing responses of the
           id-pkix-ocsp-basic response type.
    */
    internal sealed class ResponseBytes
    {
        internal Oid ResponseType { get; }
        internal ReadOnlyMemory<byte> Response { get; }

        private ResponseBytes(Oid responseType, ReadOnlyMemory<byte> response)
        {
            ResponseType = responseType;
            Response = response;
        }

        internal static ResponseBytes From(BasicOcspResponse basicResponse)
        {
            if (basicResponse is null)
            {
                throw new ArgumentNullException(nameof(basicResponse));
            }

            AsnWriter writer = new(AsnEncodingRules.DER);

            basicResponse.Encode(writer);

            byte[] response = writer.Encode();

            return new ResponseBytes(TestOids.OcspBasic, response);
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(ResponseType.Value);
                writer.WriteOctetString(Response.Span);
            }
        }
    }
}
