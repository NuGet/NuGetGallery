// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET7_0_OR_GREATER

#nullable enable

using System.Collections.Generic;
#if NET
using System.Diagnostics;
#endif
using System.Formats.Asn1;
using System.Numerics;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1;

namespace System.Security.Cryptography.X509Certificates
{
    public sealed partial class CertificateRevocationListBuilder
    {
        /// <summary>
        ///   Decodes the specified Certificate Revocation List (CRL) and produces
        ///   a <see cref="CertificateRevocationListBuilder" /> with all of the revocation
        ///   entries from the decoded CRL.
        /// </summary>
        /// <param name="currentCrl">
        ///   The DER-encoded CRL to decode.
        /// </param>
        /// <param name="currentCrlNumber">
        ///   When this method returns, contains the CRL sequence number from the decoded CRL.
        ///   This parameter is treated as uninitialized.
        /// </param>
        /// <returns>
        ///   A new builder that has the same revocation entries as the decoded CRL.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="currentCrl" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="CryptographicException">
        ///   <para>
        ///     <paramref name="currentCrl" /> could not be decoded.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="currentCrl" /> decoded successfully, but decoding did not
        ///     need all of the bytes provided in the array.
        ///   </para>
        /// </exception>
        public static CertificateRevocationListBuilder Load(ReadOnlyMemory<byte> currentCrl, out BigInteger currentCrlNumber)
        {
            CertificateRevocationListBuilder ret = Load(
                currentCrl,
                out BigInteger crlNumber,
                out int bytesConsumed);

            if (bytesConsumed != currentCrl.Length)
            {
                throw new CryptographicException("ASN1 corrupted data.");
            }

            currentCrlNumber = crlNumber;
            return ret;
        }

        /// <summary>
        ///   Decodes the specified Certificate Revocation List (CRL) and produces
        ///   a <see cref="CertificateRevocationListBuilder" /> with all of the revocation
        ///   entries from the decoded CRL.
        /// </summary>
        /// <param name="currentCrl">
        ///   The DER-encoded CRL to decode.
        /// </param>
        /// <param name="currentCrlNumber">
        ///   When this method returns, contains the CRL sequence number from the decoded CRL.
        ///   This parameter is treated as uninitialized.
        /// </param>
        /// <param name="bytesConsumed">
        ///   When this method returns, contains the number of bytes that were read from
        ///   <paramref name="currentCrl"/> while decoding.
        /// </param>
        /// <returns>
        ///   A new builder that has the same revocation entries as the decoded CRL.
        /// </returns>
        /// <exception cref="CryptographicException">
        ///   <paramref name="currentCrl" /> could not be decoded.
        /// </exception>
        public static CertificateRevocationListBuilder Load(
            ReadOnlyMemory<byte> currentCrl,
            out BigInteger currentCrlNumber,
            out int bytesConsumed)
        {
            List<RevokedCertificate> list = new();
            BigInteger crlNumber = 0;
            int payloadLength;

            try
            {
                AsnReader reader = new(currentCrl, AsnEncodingRules.DER);
                payloadLength = reader.PeekEncodedValue().Length;

                AsnReader certificateList = reader.ReadSequence();
                AsnReader tbsCertList = certificateList.ReadSequence();
                AlgorithmIdentifier.Decode(certificateList);

                if (!certificateList.TryReadPrimitiveBitString(out int _, out ReadOnlyMemory<byte> _))
                {
                    throw new CryptographicException("ASN1 corrupted data.");
                }

                certificateList.ThrowIfNotEmpty();

                int version = 0;

                if (tbsCertList.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
                {
                    // https://datatracker.ietf.org/doc/html/rfc5280#section-5.1 says the only
                    // version values are v1 (0) and v2 (1).
                    //
                    // Since v1 (0) is supposed to not write down the version value, v2 (1) is the
                    // only legal value to read.
                    if (!tbsCertList.TryReadInt32(out version) || version != 1)
                    {
                        throw new CryptographicException("ASN1 corrupted data.");
                    }
                }

                AlgorithmIdentifier.Decode(tbsCertList);

                // X500DN
                tbsCertList.ReadSequence();

                // thisUpdate
                ReadX509Time(ref tbsCertList);

                // nextUpdate
                ReadX509TimeOpt(ref tbsCertList);

                AsnReader? revokedCertificates = null;

                if (tbsCertList.HasData && tbsCertList.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
                {
                    revokedCertificates = tbsCertList.ReadSequence();
                }

                if (version > 0 && tbsCertList.HasData)
                {
                    AsnReader crlExtensionsExplicit = tbsCertList.ReadSequence(Asn1Tags.ContextSpecific0);
                    AsnReader crlExtensions = crlExtensionsExplicit.ReadSequence();
                    crlExtensionsExplicit.ThrowIfNotEmpty();

                    while (crlExtensions.HasData)
                    {
                        AsnReader extension = crlExtensions.ReadSequence();
                        Oid? extnOid = GetSharedOrNullOid(ref extension);

                        if (extnOid is null)
                        {
                            extension.ReadObjectIdentifier();
                        }

                        if (extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean))
                        {
                            extension.ReadBoolean();
                        }

                        if (!extension.TryReadPrimitiveOctetString(out ReadOnlyMemory<byte> extnValue))
                        {
                            throw new CryptographicException("ASN1 corrupted data.");
                        }

                        // Since we're only matching against OIDs that come from GetSharedOrNullOid
                        // we can use ReferenceEquals and skip the Value string equality check in
                        // the Oid.ValueEquals extension method (as it will always be preempted by
                        // the ReferenceEquals or will evaulate to false).
                        if (ReferenceEquals(extnOid, TestOids.CrlNumber))
                        {
                            AsnReader crlNumberReader = new AsnReader(
                                extnValue,
                                AsnEncodingRules.DER);

                            crlNumber = crlNumberReader.ReadInteger();
                            crlNumberReader.ThrowIfNotEmpty();
                        }
                    }
                }

                tbsCertList.ThrowIfNotEmpty();

                while (revokedCertificates is not null && revokedCertificates.HasData)
                {
                    RevokedCertificate revokedCertificate = new RevokedCertificate(ref revokedCertificates, version);
                    list.Add(revokedCertificate);
                }
            }
            catch (AsnContentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }

            bytesConsumed = payloadLength;
            currentCrlNumber = crlNumber;
            return new CertificateRevocationListBuilder(list);
        }

        private static Oid? GetSharedOrNullOid(ref AsnReader asnValueReader, Asn1Tag? expectedTag = null)
        {
#if NET
            Asn1Tag tag = asnValueReader.PeekTag();

            // This isn't a valid OID, so return null and let whatever's going to happen happen.
            if (tag.IsConstructed)
            {
                return null;
            }

            Asn1Tag expected = expectedTag.GetValueOrDefault(Asn1Tag.ObjectIdentifier);

            Debug.Assert(
                expected.TagClass != TagClass.Universal ||
                expected.TagValue == (int)UniversalTagNumber.ObjectIdentifier,
                $"{nameof(GetSharedOrNullOid)} was called with the wrong Universal class tag: {expectedTag}");

            // Not the tag we're expecting, so don't match.
            if (!tag.HasSameClassAndValue(expected))
            {
                return null;
            }

            ReadOnlySpan<byte> contentBytes = asnValueReader.PeekContentBytes().Span;

#pragma warning disable format
            Oid? ret = contentBytes switch
            {
                [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x01] => TestOids.EmailAddress,
                [0x55, 0x04, 0x03] => TestOids.CommonName,
                [0x55, 0x04, 0x06] => TestOids.CountryOrRegionName,
                [0x55, 0x04, 0x07] => TestOids.LocalityName,
                [0x55, 0x04, 0x08] => TestOids.StateOrProvinceName,
                [0x55, 0x04, 0x0A] => TestOids.Organization,
                [0x55, 0x04, 0x0B] => TestOids.OrganizationalUnit,
                [0x55, 0x1D, 0x14] => TestOids.CrlNumber,
                _ => null,
            };
#pragma warning restore format

            if (ret is not null)
            {
                // Move to the next item.
                asnValueReader.ReadEncodedValue();
            }

            return ret;
#else
            // The list pattern isn't available in System.Security.Cryptography.Pkcs for the
            // netstandard2.0 or netfx builds.  Any OIDs that it's important to optimize in
            // those contexts can be matched on here, but using a longer form of matching.

            return null;
#endif
        }
    }
}

#endif
