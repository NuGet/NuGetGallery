// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.1):

           Signature       ::=     SEQUENCE {
               signatureAlgorithm      AlgorithmIdentifier,
               signature               BIT STRING,
               certs               [0] EXPLICIT SEQUENCE OF Certificate
           OPTIONAL}
    */
    internal sealed class OcspSignature
    {
        internal AlgorithmIdentifier SignatureAlgorithm { get; }
        internal ReadOnlyMemory<byte> Signature { get; }
        internal IReadOnlyList<X509Certificate2> Certs { get; }

        private OcspSignature(
            AlgorithmIdentifier signatureAlgorithm,
            ReadOnlyMemory<byte> signature,
            IReadOnlyList<X509Certificate2> certs)
        {
            SignatureAlgorithm = signatureAlgorithm;
            Signature = signature;
            Certs = certs;
        }

        internal static OcspSignature Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            AlgorithmIdentifier signatureAlgorithm = AlgorithmIdentifier.Decode(sequenceReader);
            ReadOnlyMemory<byte> signature = sequenceReader.ReadBitString(out _);
            List<X509Certificate2> certs = new();

            if (sequenceReader.HasData)
            {
                Asn1Tag certsTag = new(TagClass.ContextSpecific, tagValue: 0);

                if (!sequenceReader.PeekTag().HasSameClassAndValue(certsTag))
                {
                    throw new InvalidAsn1Exception();
                }

                AsnReader certsSequenceReader = sequenceReader.ReadSequence();

                while (certsSequenceReader.HasData)
                {
                    ReadOnlyMemory<byte> data = certsSequenceReader.ReadEncodedValue();
#if NET9_0_OR_GREATER
                    X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(data.Span.ToArray());
#else
                    X509Certificate2 certificate = new(data.Span.ToArray());
#endif
                    certs.Add(certificate);
                }
            }

            return new OcspSignature(signatureAlgorithm, signature, certs);
        }
    }
}
