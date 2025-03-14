// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// Copied and pared down from https://github.com/dotnet/runtime/blob/cfa42d32b45780c85a407b5dfde4bb7d30433da8/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/X509Certificates/X509AuthorityKeyIdentifierExtension.cs

#if !NET7_0_OR_GREATER

#nullable enable

using System.Formats.Asn1;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1;

namespace System.Security.Cryptography.X509Certificates
{
    internal sealed class X509AuthorityKeyIdentifierExtension : X509Extension
    {
        private X500DistinguishedName? _simpleIssuer;
        private ReadOnlyMemory<byte>? _keyIdentifier;
        private ReadOnlyMemory<byte>? _rawIssuer;
        private ReadOnlyMemory<byte>? _serialNumber;

        public X509AuthorityKeyIdentifierExtension(byte[] rawData, bool critical = false)
            : base(TestOids.AuthorityKeyIdentifier, rawData, critical)
        {
            Decode(RawData);
        }

        public X509AuthorityKeyIdentifierExtension(ReadOnlySpan<byte> rawData, bool critical = false)
            : base(TestOids.AuthorityKeyIdentifier, rawData.ToArray(), critical)
        {
            Decode(RawData);
        }

        public override void CopyFrom(AsnEncodedData asnEncodedData)
        {
            base.CopyFrom(asnEncodedData);
        }

        public static X509AuthorityKeyIdentifierExtension CreateFromSubjectKeyIdentifier(
            ReadOnlySpan<byte> subjectKeyIdentifier)
        {
            AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                writer.WriteOctetString(subjectKeyIdentifier, Asn1Tags.ContextSpecific0);
            }

            // Most KeyIdentifier values are computed from SHA-1 (20 bytes), which produces a 24-byte
            // value for this extension.
            // Let's go ahead and be really generous before moving to redundant array allocation.
            Span<byte> stackSpan = stackalloc byte[64];
            scoped ReadOnlySpan<byte> encoded;

            if (writer.TryEncode(stackSpan, out int written))
            {
                encoded = stackSpan.Slice(0, written);
            }
            else
            {
                encoded = writer.Encode();
            }

            return new X509AuthorityKeyIdentifierExtension(encoded);
        }

        public static X509AuthorityKeyIdentifierExtension CreateFromIssuerNameAndSerialNumber(
            X500DistinguishedName issuerName,
            ReadOnlySpan<byte> serialNumber)
        {
            if (issuerName is null)
            {
                throw new ArgumentNullException(nameof(issuerName));
            }

            AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                using (writer.PushSequence(Asn1Tags.ContextSpecific4))
                {
                    writer.WriteEncodedValue(issuerName.RawData);
                }

                try
                {
                    writer.WriteInteger(serialNumber, Asn1Tags.ContextSpecific2);
                }
                catch (ArgumentException)
                {
                    ThrowInvalidSerialNumberBytes(nameof(serialNumber));
                }
            }

            return new X509AuthorityKeyIdentifierExtension(writer.Encode());
        }

        public static X509AuthorityKeyIdentifierExtension Create(
            byte[] keyIdentifier,
            X500DistinguishedName issuerName,
            byte[] serialNumber)
        {
            if (keyIdentifier is null)
            {
                throw new ArgumentNullException(nameof(keyIdentifier));
            }

            if (issuerName is null)
            {
                throw new ArgumentNullException(nameof(issuerName));
            }

            if (serialNumber is null)
            {
                throw new ArgumentNullException(nameof(serialNumber));
            }

            return Create(
                new ReadOnlySpan<byte>(keyIdentifier),
                issuerName,
                new ReadOnlySpan<byte>(serialNumber));
        }

        public static X509AuthorityKeyIdentifierExtension Create(
            ReadOnlySpan<byte> keyIdentifier,
            X500DistinguishedName issuerName,
            ReadOnlySpan<byte> serialNumber)
        {
            if (issuerName is null)
            {
                throw new ArgumentNullException(nameof(issuerName));
            }

            AsnWriter writer = new AsnWriter(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                writer.WriteOctetString(keyIdentifier, Asn1Tags.ContextSpecific0);

                using (writer.PushSequence(Asn1Tags.ContextSpecific1))
                using (writer.PushSequence(Asn1Tags.ContextSpecific4))
                {
                    writer.WriteEncodedValue(issuerName.RawData);
                }

                try
                {
                    writer.WriteInteger(serialNumber, Asn1Tags.ContextSpecific2);
                }
                catch (ArgumentException)
                {
                    ThrowInvalidSerialNumberBytes(nameof(serialNumber));
                }
            }

            return new X509AuthorityKeyIdentifierExtension(writer.Encode());
        }

        public static X509AuthorityKeyIdentifierExtension CreateFromCertificate(
            X509Certificate2 certificate,
            bool includeKeyIdentifier,
            bool includeIssuerAndSerial)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            if (includeKeyIdentifier)
            {
                X509SubjectKeyIdentifierExtension? skid =
                    (X509SubjectKeyIdentifierExtension?)certificate.Extensions[TestOids.SubjectKeyIdentifier.Value!];

                if (skid is null)
                {
                    throw new CryptographicException("The provided certificate does not have a Subject Key Identifier extension.");
                }

                ReadOnlySpan<byte> skidBytes = HexConverter.ToByteArray(skid.SubjectKeyIdentifier);

                if (includeIssuerAndSerial)
                {
                    return Create(
                        skidBytes,
                        certificate.IssuerName,
                        certificate.GetSerialNumberBigEndian());
                }

                return CreateFromSubjectKeyIdentifier(skidBytes);
            }
            else if (includeIssuerAndSerial)
            {
                return CreateFromIssuerNameAndSerialNumber(
                    certificate.IssuerName,
                    certificate.GetSerialNumberBigEndian());
            }

            ReadOnlySpan<byte> emptyExtension = new byte[] { 0x30, 0x00 };
            return new X509AuthorityKeyIdentifierExtension(emptyExtension);
        }

        private void Decode(ReadOnlySpan<byte> rawData)
        {
            _keyIdentifier = null;
            _simpleIssuer = null;
            _rawIssuer = null;
            _serialNumber = null;

            // https://datatracker.ietf.org/doc/html/rfc3280#section-4.2.1.1
            // AuthorityKeyIdentifier ::= SEQUENCE {
            //    keyIdentifier[0] KeyIdentifier OPTIONAL,
            //    authorityCertIssuer[1] GeneralNames OPTIONAL,
            //    authorityCertSerialNumber[2] CertificateSerialNumber OPTIONAL  }
            //
            // KeyIdentifier::= OCTET STRING

            try
            {
                AsnReader reader = new AsnReader(rawData.ToArray(), AsnEncodingRules.DER);
                AsnReader aki = reader.ReadSequence();
                reader.ThrowIfNotEmpty();

                Asn1Tag nextTag = default;

                if (aki.HasData)
                {
                    nextTag = aki.PeekTag();
                }

                if (nextTag.HasSameClassAndValue(Asn1Tags.ContextSpecific0))
                {
                    _keyIdentifier = aki.ReadOctetString(nextTag);

                    if (aki.HasData)
                    {
                        nextTag = aki.PeekTag();
                    }
                }

                if (nextTag.HasSameClassAndValue(Asn1Tags.ContextSpecific1))
                {
                    byte[] rawIssuer = aki.PeekEncodedValue().ToArray();
                    _rawIssuer = rawIssuer;

                    AsnReader generalNames = aki.ReadSequence(nextTag);
                    bool foundIssuer = false;

                    // Walk all of the entities to make sure they decode legally, so no early abort.
                    while (generalNames.HasData)
                    {
                        GeneralName generalName = GeneralName.Decode(generalNames);

                        if (generalName.DirectoryName is not null)
                        {
                            if (!foundIssuer)
                            {
                                // Only ever try reading the first one.
                                // Don't just use a null check or we would load the last of an odd number.
                                foundIssuer = true;

                                _simpleIssuer = new X500DistinguishedName(
                                    generalName.DirectoryName!.Value.ToArray());
                            }
                            else
                            {
                                _simpleIssuer = null;
                            }
                        }
                    }

                    if (aki.HasData)
                    {
                        nextTag = aki.PeekTag();
                    }
                }

                if (nextTag.HasSameClassAndValue(Asn1Tags.ContextSpecific2))
                {
                    _serialNumber = aki.ReadIntegerBytes(nextTag).ToArray();
                }

                aki.ThrowIfNotEmpty();
            }
            catch (AsnContentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }
        }

        private static void ThrowInvalidSerialNumberBytes(string parameterName)
        {
            throw new ArgumentException("The provided serial number is invalid. Ensure the input is in big-endian byte order and that all redundant leading bytes have been removed.", parameterName);
        }
    }
}

#endif
