// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 6960 (https://datatracker.ietf.org/doc/html/rfc6960#section-4.1):

           CertID          ::=     SEQUENCE {
               hashAlgorithm       AlgorithmIdentifier,
               issuerNameHash      OCTET STRING, -- Hash of issuer's DN
               issuerKeyHash       OCTET STRING, -- Hash of issuer's public key
               serialNumber        CertificateSerialNumber }
    */
    internal sealed class CertId
    {
        internal AlgorithmIdentifier AlgorithmIdentifier { get; }
        internal ReadOnlyMemory<byte> IssuerNameHash { get; }
        internal ReadOnlyMemory<byte> IssuerKeyHash { get; }
        internal BigInteger SerialNumber { get; }

        private CertId(
            AlgorithmIdentifier algorithmIdentifier,
            ReadOnlyMemory<byte> issuerNameHash,
            ReadOnlyMemory<byte> issuerKeyHash,
            BigInteger serialNumber)
        {
            AlgorithmIdentifier = algorithmIdentifier;
            IssuerNameHash = issuerNameHash;
            IssuerKeyHash = issuerKeyHash;
            SerialNumber = serialNumber;
        }

        internal static CertId FromCertificates(X509Certificate2 issuerCertificate, X509Certificate2 certificate)
        {
            AlgorithmIdentifier algorithmIdentifier = new(TestOids.Sha256);
            ReadOnlyMemory<byte> issuerNameHash;
            ReadOnlyMemory<byte> issuerKeyHash;

            using (SHA256 sha256 = SHA256.Create())
            {
                issuerNameHash = sha256.ComputeHash(issuerCertificate.IssuerName.RawData);
                issuerKeyHash = sha256.ComputeHash(issuerCertificate.PublicKey.EncodedKeyValue.RawData);
            }

            BigInteger serialNumber = new(certificate.GetSerialNumber());

            return new CertId(
                algorithmIdentifier,
                issuerNameHash,
                issuerKeyHash,
                serialNumber);
        }

        internal static CertId Decode(AsnReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            AsnReader sequenceReader = reader.ReadSequence();
            AlgorithmIdentifier algorithmIdentifier = AlgorithmIdentifier.Decode(sequenceReader);
            ReadOnlyMemory<byte> issuerNameHash = sequenceReader.ReadOctetString();
            ReadOnlyMemory<byte> issuerKeyHash = sequenceReader.ReadOctetString();
            BigInteger serialNumber = sequenceReader.ReadInteger();

            return new CertId(
                algorithmIdentifier,
                issuerNameHash,
                issuerKeyHash,
                serialNumber);
        }

        internal void Encode(AsnWriter writer)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            using (writer.PushSequence())
            {
                AlgorithmIdentifier.Encode(writer);
                writer.WriteOctetString(IssuerNameHash.Span);
                writer.WriteOctetString(IssuerKeyHash.Span);
                writer.WriteInteger(SerialNumber);
            }
        }
    }
}
