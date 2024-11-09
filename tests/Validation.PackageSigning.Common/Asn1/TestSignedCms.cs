// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1
{
    /*
        From RFC 5652 (https://datatracker.ietf.org/doc/html/rfc5652#section-3 and https://datatracker.ietf.org/doc/html/rfc5652#section-5.1):

          ContentInfo ::= SEQUENCE {
            contentType ContentType,
            content [0] EXPLICIT ANY DEFINED BY contentType }

          ContentType ::= OBJECT IDENTIFIER

          id-signedData OBJECT IDENTIFIER ::= { iso(1) member-body(2)
             us(840) rsadsi(113549) pkcs(1) pkcs7(7) 2 }

          SignedData ::= SEQUENCE {
            version CMSVersion,
            digestAlgorithms DigestAlgorithmIdentifiers,
            encapContentInfo EncapsulatedContentInfo,
            certificates [0] IMPLICIT CertificateSet OPTIONAL,
            crls [1] IMPLICIT RevocationInfoChoices OPTIONAL,
            signerInfos SignerInfos }

          DigestAlgorithmIdentifiers ::= SET OF DigestAlgorithmIdentifier

          SignerInfos ::= SET OF SignerInfo
    */
    public sealed class TestSignedCms
    {
        private readonly ReadOnlyMemory<byte> _version;
        private readonly ReadOnlyMemory<byte> _digestAlgorithms;
        private readonly ReadOnlyMemory<byte> _encapContentInfo;
        private readonly ReadOnlyMemory<byte>? _crls;
        private readonly List<TestSignerInfo> _signerInfos;

        public IReadOnlyList<TestSignerInfo> SignerInfos => _signerInfos;

        public X509Certificate2Collection? Certificates { get; private set; }

        private TestSignedCms(
            ReadOnlyMemory<byte> version,
            ReadOnlyMemory<byte> digestAlgorithms,
            ReadOnlyMemory<byte> encapContentInfo,
            X509Certificate2Collection? certificates,
            ReadOnlyMemory<byte>? crls,
            List<TestSignerInfo> signerInfos)
        {
            _version = version;
            _digestAlgorithms = digestAlgorithms;
            _encapContentInfo = encapContentInfo;
            Certificates = certificates;
            _crls = crls;
            _signerInfos = signerInfos;
        }

        public static TestSignedCms Decode(SignedCms signedCms)
        {
            if (signedCms is null)
            {
                throw new ArgumentNullException(nameof(signedCms));
            }

            byte[] bytes = signedCms.Encode();

            return Decode(bytes);
        }

        public static TestSignedCms Decode(ReadOnlyMemory<byte> bytes)
        {
            AsnReader reader = new(bytes, AsnEncodingRules.DER);
            AsnReader contentInfoSequenceReader = reader.ReadSequence();

            string contentType = contentInfoSequenceReader.ReadObjectIdentifier();

            if (!string.Equals(TestOids.SignedData.Value, contentType))
            {
                throw new InvalidAsn1Exception();
            }

            AsnReader explicitContentReader = contentInfoSequenceReader.ReadSequence(Asn1Tags.ContextSpecific0);
            AsnReader signedDataSequenceReader = explicitContentReader.ReadSequence();

            ReadOnlyMemory<byte> version = signedDataSequenceReader.ReadEncodedValue();
            ReadOnlyMemory<byte> digestAlgorithms = signedDataSequenceReader.ReadEncodedValue();
            ReadOnlyMemory<byte> encapContentInfo = signedDataSequenceReader.ReadEncodedValue();
            X509Certificate2Collection? certificates = null;
            ReadOnlyMemory<byte>? crls = null;

            if (signedDataSequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific0))
            {
                AsnReader certificatesReader = signedDataSequenceReader.ReadSetOf(Asn1Tags.ContextSpecific0);

                while (certificatesReader.HasData)
                {
                    ReadOnlyMemory<byte> value = certificatesReader.ReadEncodedValue();
#if NET9_0_OR_GREATER
                    X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(value.Span.ToArray());
#else
                    X509Certificate2 certificate = new(value.Span.ToArray());
#endif
                    certificates ??= new X509Certificate2Collection();

                    certificates.Add(certificate);
                }
            }

            if (signedDataSequenceReader.PeekTag().HasSameClassAndValue(Asn1Tags.ContextSpecific1))
            {
                crls = signedDataSequenceReader.ReadEncodedValue();
            }

            AsnReader signerInfosReader = signedDataSequenceReader.ReadSetOf();
            List<TestSignerInfo> signerInfos = new();

            while (signerInfosReader.HasData)
            {
                TestSignerInfo testSignerInfo = TestSignerInfo.Decode(signerInfosReader);

                signerInfos.Add(testSignerInfo);
            }

            return new TestSignedCms(
                version,
                digestAlgorithms,
                encapContentInfo,
                certificates,
                crls,
                signerInfos);
        }

        public SignedCms Encode()
        {
            AsnWriter writer = new(AsnEncodingRules.DER);

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(TestOids.SignedData.Value!);

                using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                using (writer.PushSequence())
                {
                    writer.WriteEncodedValue(_version.Span);
                    writer.WriteEncodedValue(_digestAlgorithms.Span);
                    writer.WriteEncodedValue(_encapContentInfo.Span);

                    if (Certificates?.Count > 0)
                    {
                        using (writer.PushSetOf(Asn1Tags.ContextSpecific0))
                        {
                            foreach (X509Certificate2 certificate in Certificates)
                            {
                                writer.WriteEncodedValue(certificate.RawData);
                            }
                        }
                    }

                    if (_crls.HasValue)
                    {
                        writer.WriteEncodedValue(_crls.Value.Span);
                    }

                    using (writer.PushSetOf())
                    {
                        foreach (TestSignerInfo signerInfo in _signerInfos)
                        {
                            signerInfo.Encode(writer);
                        }
                    }
                }
            }

            byte[] bytes = writer.Encode();
            SignedCms signedCms = new();

            signedCms.Decode(bytes);

            return signedCms;
        }
    }
}
