// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.2.1):

           BasicOCSPResponse       ::= SEQUENCE {
              tbsResponseData      ResponseData,
              signatureAlgorithm   AlgorithmIdentifier,
              signature            BIT STRING,
              certs            [0] EXPLICIT SEQUENCE OF Certificate OPTIONAL }
    */
    internal sealed class BasicOcspResponse
    {
        internal ResponseData ResponseData { get; }
        internal AlgorithmIdentifier SignatureAlgorithm { get; }
        internal ReadOnlyMemory<byte> Signature { get; }
        internal IReadOnlyList<X509Certificate2> Certs { get; }

        internal BasicOcspResponse(
            ResponseData responseData,
            AlgorithmIdentifier signatureAlgorithm,
            ReadOnlyMemory<byte> signature,
            IReadOnlyList<X509Certificate2> certs)
        {
            ResponseData = responseData;
            SignatureAlgorithm = signatureAlgorithm;
            Signature = signature;
            Certs = certs;
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                ResponseData.Encode(writer);
                SignatureAlgorithm.Encode(writer);
                writer.WriteBitString(Signature.Span);

                if (Certs is not null && Certs.Count > 0)
                {
                    using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                    using (writer.PushSequence())
                    {
                        foreach (X509Certificate2 cert in Certs)
                        {
                            writer.WriteEncodedValue(cert.RawData);
                        }
                    }
                }
            }
        }
    }
}
