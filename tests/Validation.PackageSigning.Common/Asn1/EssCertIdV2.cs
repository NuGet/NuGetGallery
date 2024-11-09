// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5035 (https://datatracker.ietf.org/doc/html/rfc5035):

            ESSCertIDv2 ::= SEQUENCE {
                hashAlgorithm            AlgorithmIdentifier
                       DEFAULT {algorithm id-sha256},
                certHash                 Hash,
                issuerSerial             IssuerSerial OPTIONAL
            }

            Hash ::= OCTET STRING

            IssuerSerial ::= SEQUENCE {
                issuer                   GeneralNames,
                serialNumber             CertificateSerialNumber
           }
    */
    public sealed class EssCertIdV2
    {
        public AlgorithmIdentifier? HashAlgorithm { get; }
        public ReadOnlyMemory<byte> CertificateHash { get; }
        public IssuerSerial? IssuerSerial { get; }

        public EssCertIdV2(
            AlgorithmIdentifier? hashAlgorithm,
            ReadOnlyMemory<byte> hash,
            IssuerSerial? issuerSerial = null)
        {
            HashAlgorithm = hashAlgorithm;
            CertificateHash = hash;
            IssuerSerial = issuerSerial;
        }

        public static EssCertIdV2 Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            Asn1Tag tag = sequenceReader.PeekTag();
            AlgorithmIdentifier algorithmIdentifier;

            if (tag.IsConstructed && tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.Sequence, isConstructed: true)))
            {
                algorithmIdentifier = AlgorithmIdentifier.Decode(sequenceReader);
            }
            else
            {
                algorithmIdentifier = new AlgorithmIdentifier(TestOids.Sha256);
            }

            ReadOnlyMemory<byte> hash = sequenceReader.ReadOctetString();
            IssuerSerial? issuerSerial = null;

            if (sequenceReader.HasData)
            {
                issuerSerial = IssuerSerial.Decode(sequenceReader);
            }

            sequenceReader.ThrowIfNotEmpty();

            return new EssCertIdV2(algorithmIdentifier, hash, issuerSerial);
        }

        public void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                if (HashAlgorithm is not null)
                {
                    HashAlgorithm.Encode(writer);
                }

                writer.WriteOctetString(CertificateHash.Span);

                if (IssuerSerial is not null)
                {
                    IssuerSerial.Encode(writer);
                }
            }
        }

        internal static EssCertIdV2 Create(HashAlgorithmName hashAlgorithmName, X509Certificate2 certificate)
        {
            AlgorithmIdentifier algorithmIdentifier;
            HashAlgorithm hashAlgorithm;

            if (hashAlgorithmName == HashAlgorithmName.SHA256)
            {
                hashAlgorithm = SHA256.Create();
                algorithmIdentifier = new AlgorithmIdentifier(TestOids.Sha256);
            }
            else if (hashAlgorithmName == HashAlgorithmName.SHA384)
            {
                hashAlgorithm = SHA384.Create();
                algorithmIdentifier = new AlgorithmIdentifier(TestOids.Sha384);
            }
            else if (hashAlgorithmName == HashAlgorithmName.SHA512)
            {
                hashAlgorithm = SHA512.Create();
                algorithmIdentifier = new AlgorithmIdentifier(TestOids.Sha512);
            }
            else
            {
                throw new NotImplementedException();
            }

            ReadOnlyMemory<byte> hash;

            using (hashAlgorithm)
            {
                hash = hashAlgorithm.ComputeHash(certificate.RawData);
            }

            IssuerSerial? issuerSerial = null;

            if (certificate is not null)
            {
                issuerSerial = IssuerSerial.Create(certificate);
            }

            return new EssCertIdV2(algorithmIdentifier, hash, issuerSerial);
        }
    }
}
