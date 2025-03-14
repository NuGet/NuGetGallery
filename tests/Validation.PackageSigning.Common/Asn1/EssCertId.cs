// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 2634 (https://datatracker.ietf.org/doc/html/rfc2634#section-5.4.1):

            ESSCertID ::=  SEQUENCE {
                certHash                 Hash,
                issuerSerial             IssuerSerial OPTIONAL
            }

            Hash ::= OCTET STRING -- SHA1 hash of entire certificate

            IssuerSerial ::= SEQUENCE {
                issuer                   GeneralNames,
                serialNumber             CertificateSerialNumber
            }
    */
    public sealed class EssCertId
    {
        public ReadOnlyMemory<byte> CertificateHash { get; }
        public IssuerSerial? IssuerSerial { get; }

        private EssCertId(ReadOnlyMemory<byte> hash, IssuerSerial? issuerSerial)
        {
            CertificateHash = hash;
            IssuerSerial = issuerSerial;
        }

        public static EssCertId Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            ReadOnlyMemory<byte> hash = sequenceReader.ReadOctetString();
            IssuerSerial? issuerSerial = null;

            if (sequenceReader.HasData)
            {
                issuerSerial = IssuerSerial.Decode(sequenceReader);
            }

            return new EssCertId(hash, issuerSerial);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                writer.WriteOctetString(CertificateHash.Span);

                if (IssuerSerial is not null)
                {
                    IssuerSerial.Encode(writer);
                }
            }
        }

        public static EssCertId Create(ReadOnlyMemory<byte> hash, X509Certificate2? certificate = null)
        {
            IssuerSerial? issuerSerial = null;

            if (certificate is not null)
            {
                issuerSerial = IssuerSerial.Create(certificate);
            }

            return new EssCertId(hash, issuerSerial);
        }
    }
}
