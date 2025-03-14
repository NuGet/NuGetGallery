// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET7_0_OR_GREATER

#nullable enable

using System.Formats.Asn1;
using System.Numerics;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Microsoft.Internal.NuGet.Testing.SignedPackages.Asn1;

namespace System.Security.Cryptography.X509Certificates
{
    public sealed partial class CertificateRevocationListBuilder
    {
        /// <summary>
        ///   Builds a Certificate Revocation List (CRL) signed by the specified certificate.
        /// </summary>
        /// <param name="issuerCertificate">
        ///   The certificate representing the Certificate Authority (CA) that is creating
        ///   this Certificate Revocation List.
        /// </param>
        /// <param name="crlNumber">
        ///   The sequence number for this CRL.  Per IETF RFC 5280, this value must always
        ///   increase from one CRL to the next for a given CA.
        /// </param>
        /// <param name="nextUpdate">
        ///   The latest possible time before the CA will publish a newer CRL, generally
        ///   treated as an expiration date for this CRL.
        /// </param>
        /// <param name="hashAlgorithm">
        ///   The hash algorithm to use when signing the CRL.
        /// </param>
        /// <param name="rsaSignaturePadding">
        ///   For Certificate Authorities with RSA keys, this parameter is required and specifies
        ///   the RSA signature padding mode to use when signing the CRL.
        ///   For all other algorithms, this parameter is ignored.
        ///   The default is <see langword="null"/>.
        /// </param>
        /// <param name="thisUpdate">
        ///   An optional value that specifies when this CRL was created, or
        ///   <see langword="null"/> to use the current system time.
        ///   The default is <see langword="null" />.
        /// </param>
        /// <returns>
        ///   An array that contains the bytes of the signed CRL.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <para>
        ///     <paramref name="issuerCertificate" /> is <see langword="null" />.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="hashAlgorithm" /> has <see langword="null" /> as the value of
        ///     <see cref="HashAlgorithmName.Name"/>.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///     <paramref name="issuerCertificate"/> does not have an associated private key.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> uses a public key algorithm that is unknown,
        ///     or not supported by this implementation.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> does not have a Basic Constraints extension.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> has a Basic Constraints extension that indicates
        ///     it is not a valid Certificate Authority certificate.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> has a Key Usage extension that lacks the
        ///     <see cref="X509KeyUsageFlags.CrlSign" /> usage.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> has an RSA private key but
        ///     <paramref name="rsaSignaturePadding"/> is <see langword="null" />.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="issuerCertificate"/> has an unknown key algorithm.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="nextUpdate"/> is older than <paramref name="thisUpdate"/>.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="hashAlgorithm" /> has the empty string as the value of
        ///     <see cref="HashAlgorithmName.Name"/>.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="rsaSignaturePadding"/> was not recognized.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="crlNumber"/> is negative.
        /// </exception>
        /// <exception cref="CryptographicException">
        ///   an error occurred during signing.
        /// </exception>
        public byte[] Build(
            X509Certificate2 issuerCertificate,
            BigInteger crlNumber,
            DateTimeOffset nextUpdate,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding? rsaSignaturePadding = null,
            DateTimeOffset? thisUpdate = null)
        {
            return Build(
                issuerCertificate,
                crlNumber,
                nextUpdate,
                thisUpdate.GetValueOrDefault(DateTimeOffset.UtcNow),
                hashAlgorithm,
                rsaSignaturePadding);
        }

        private byte[] Build(
            X509Certificate2 issuerCertificate,
            BigInteger crlNumber,
            DateTimeOffset nextUpdate,
            DateTimeOffset thisUpdate,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding? rsaSignaturePadding)
        {
            if (issuerCertificate is null)
            {
                throw new ArgumentNullException(nameof(issuerCertificate));
            }

            if (!issuerCertificate.HasPrivateKey)
                throw new ArgumentException(
                    "SR.Cryptography_CertReq_IssuerRequiresPrivateKey",
                    nameof(issuerCertificate));
            if (crlNumber < BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(crlNumber));
            }
            if (nextUpdate <= thisUpdate)
                throw new ArgumentException("The provided thisUpdate value is later than the nextUpdate value.");

            if (string.IsNullOrEmpty(hashAlgorithm.Name))
            {
                throw new ArgumentException(message: null, nameof(issuerCertificate));
            }

            // Check the Basic Constraints and Key Usage extensions to help identify inappropriate certificates.
            // Note that this is not a security check. The system library backing X509Chain will use these same criteria
            // to determine if the CRL is valid; and a user can easily call the X509SignatureGenerator overload to
            // bypass this validation.  We're simply helping them at signing time understand that they've
            // chosen the wrong cert.
            var basicConstraints = (X509BasicConstraintsExtension?)issuerCertificate.Extensions[TestOids.BasicConstraints2.Value!];
            var keyUsage = (X509KeyUsageExtension?)issuerCertificate.Extensions[TestOids.KeyUsage.Value!];
            var subjectKeyIdentifier =
                (X509SubjectKeyIdentifierExtension?)issuerCertificate.Extensions[TestOids.SubjectKeyIdentifier.Value!];

            if (basicConstraints == null)
            {
                throw new ArgumentException(
                    "The issuer certificate does not have a Basic Constraints extension.",
                    nameof(issuerCertificate));
            }

            if (!basicConstraints.CertificateAuthority)
            {
                throw new ArgumentException(
                    "The issuer certificate does not have an appropriate value for the Basic Constraints extension.",
                    nameof(issuerCertificate));
            }

            if (keyUsage != null && (keyUsage.KeyUsages & X509KeyUsageFlags.CrlSign) == 0)
            {
                throw new ArgumentException(
                    "The issuer certificate's Key Usage extension is present but does not contain the CrlSign flag.",
                    nameof(issuerCertificate));
            }

            AsymmetricAlgorithm? key = null;
            string keyAlgorithm = issuerCertificate.GetKeyAlgorithm();
            X509SignatureGenerator generator;

            try
            {
                if (string.Equals(keyAlgorithm, TestOids.Rsa.Value))
                {
                    if (rsaSignaturePadding is null)
                    {
                        throw new ArgumentException("The issuer certificate uses an RSA key but no RSASignaturePadding was provided to a constructor. If one cannot be provided, use the X509SignatureGenerator overload.");
                    }

                    RSA? rsa = issuerCertificate.GetRSAPrivateKey();
                    key = rsa;
                    generator = X509SignatureGenerator.CreateForRSA(rsa!, rsaSignaturePadding);
                }
                else if (string.Equals(keyAlgorithm, TestOids.EcPublicKey.Value))
                {
                    ECDsa? ecdsa = issuerCertificate.GetECDsaPrivateKey();
                    key = ecdsa;
                    generator = X509SignatureGenerator.CreateForECDsa(ecdsa!);
                }
                else
                {
                    throw new ArgumentException(
                        string.Format("'{0}' is not a known key algorithm.", keyAlgorithm),
                        nameof(issuerCertificate));
                }

                X509AuthorityKeyIdentifierExtension akid;

                if (subjectKeyIdentifier is not null)
                {
                    byte[] bytes = HexConverter.ToByteArray(subjectKeyIdentifier.SubjectKeyIdentifier);
                    akid = X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(bytes);
                }
                else
                {
                    ReadOnlySpan<byte> serialNumber = issuerCertificate.GetSerialNumberBigEndian();

                    akid = X509AuthorityKeyIdentifierExtension.CreateFromIssuerNameAndSerialNumber(
                        issuerCertificate.IssuerName,
                        serialNumber);
                }

                return Build(
                    issuerCertificate.SubjectName,
                    generator,
                    crlNumber,
                    nextUpdate,
                    thisUpdate,
                    hashAlgorithm,
                    akid);
            }
            finally
            {
                key?.Dispose();
            }
        }

        /// <summary>
        ///   Builds a Certificate Revocation List (CRL).
        /// </summary>
        /// <param name="issuerName">
        ///   The subject name of the certificate for the Certificate Authority (CA) that is
        ///   issuing this CRL.
        /// </param>
        /// <param name="generator">
        ///   A signature generator to produce the CA signature for this CRL.
        /// </param>
        /// <param name="crlNumber">
        ///   The sequence number for this CRL.  Per IETF RFC 5280, this value must always
        ///   increase from one CRL to the next for a given CA.
        /// </param>
        /// <param name="nextUpdate">
        ///   The latest possible time before the CA will publish a newer CRL, generally
        ///   treated as an expiration date for this CRL.
        /// </param>
        /// <param name="hashAlgorithm">
        ///   The hash algorithm to use when signing the CRL.
        /// </param>
        /// <param name="authorityKeyIdentifier">
        ///   The Authority Key Identifier to use in this CRL, identifying the CA certificate.
        /// </param>
        /// <param name="thisUpdate">
        ///   An optional value that specifies when this CRL was created, or
        ///   <see langword="null" /> to use the current system time.
        ///   The default is <see langword="null" />.
        /// </param>
        /// <returns>
        ///   An array that contains the bytes of the signed CRL.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <para>
        ///     <paramref name="issuerName" />, <paramref name="generator" />, or
        ///     <paramref name="authorityKeyIdentifier" /> is <see langword="null" />.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="hashAlgorithm" /> has <see langword="null" /> as the value of
        ///     <see cref="HashAlgorithmName.Name"/>.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///     <paramref name="nextUpdate"/> is older than <paramref name="thisUpdate"/>.
        ///   </para>
        ///   <para>- or -</para>
        ///   <para>
        ///     <paramref name="hashAlgorithm" /> has the empty string as the value of
        ///     <see cref="HashAlgorithmName.Name"/>.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="crlNumber"/> is negative.
        /// </exception>
        /// <exception cref="CryptographicException">
        ///   an error occurred during signing.
        /// </exception>
        internal byte[] Build(
            X500DistinguishedName issuerName,
            X509SignatureGenerator generator,
            BigInteger crlNumber,
            DateTimeOffset nextUpdate,
            HashAlgorithmName hashAlgorithm,
            X509AuthorityKeyIdentifierExtension authorityKeyIdentifier,
            DateTimeOffset? thisUpdate = null)
        {
            return Build(
                issuerName,
                generator,
                crlNumber,
                nextUpdate,
                thisUpdate.GetValueOrDefault(DateTimeOffset.UtcNow),
                hashAlgorithm,
                authorityKeyIdentifier);
        }

        private byte[] Build(
            X500DistinguishedName issuerName,
            X509SignatureGenerator generator,
            BigInteger crlNumber,
            DateTimeOffset nextUpdate,
            DateTimeOffset thisUpdate,
            HashAlgorithmName hashAlgorithm,
            X509AuthorityKeyIdentifierExtension authorityKeyIdentifier)
        {
            if (issuerName is null)
            {
                throw new ArgumentNullException(nameof(issuerName));
            }

            if (generator is null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (crlNumber < BigInteger.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(crlNumber));
            }

            if (nextUpdate <= thisUpdate)
                throw new ArgumentException("The provided thisUpdate value is later than the nextUpdate value.");

            if (string.IsNullOrEmpty(hashAlgorithm.Name))
            {
                throw new ArgumentException(message: null, nameof(hashAlgorithm));
            }

            if (authorityKeyIdentifier is null)
            {
                throw new ArgumentNullException(nameof(authorityKeyIdentifier));
            }

            byte[] signatureAlgId = generator.GetSignatureAlgorithmIdentifier(hashAlgorithm);
            AsnReader reader = new(signatureAlgId, AsnEncodingRules.DER);

            {
                AlgorithmIdentifier signatureAlgorithmAsn;

                // Deserialization also does validation of the value (except for Parameters,
                // which have to be validated separately).
                signatureAlgorithmAsn = AlgorithmIdentifier.Decode(reader);

                if (signatureAlgorithmAsn.Parameters.HasValue)
                {
                    ValidateDer(signatureAlgorithmAsn.Parameters.GetValueOrDefault());
                }
            }

            AsnWriter writer = (_writer ??= new AsnWriter(AsnEncodingRules.DER));
            writer.Reset();

            // TBSCertList
            using (writer.PushSequence())
            {
                // version v2(1)
                writer.WriteInteger(1);

                // signature (AlgorithmIdentifier)
                writer.WriteEncodedValue(signatureAlgId);

                // issuer
                writer.WriteEncodedValue(issuerName.RawData);

                // thisUpdate
                WriteX509Time(writer, thisUpdate);

                // nextUpdate
                WriteX509Time(writer, nextUpdate);

                // revokedCertificates (don't write down if empty)
                if (_revoked.Count > 0)
                {
                    // SEQUENCE OF
                    using (writer.PushSequence())
                    {
                        foreach (RevokedCertificate revoked in _revoked)
                        {
                            // Anonymous CRL Entry type
                            using (writer.PushSequence())
                            {
                                writer.WriteInteger(revoked.Serial);
                                WriteX509Time(writer, revoked.RevocationTime);

                                if (revoked.Extensions is not null)
                                {
                                    writer.WriteEncodedValue(revoked.Extensions);
                                }
                            }
                        }
                    }
                }

                // extensions [0] EXPLICIT Extensions
                using (writer.PushSequence(Asn1Tags.ContextSpecific0))
                {
                    // Extensions (SEQUENCE OF)
                    using (writer.PushSequence())
                    {
                        // Authority Key Identifier Extension
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier(authorityKeyIdentifier.Oid!.Value!);

                            if (authorityKeyIdentifier.Critical)
                            {
                                writer.WriteBoolean(true);
                            }

                            byte[] encodedAkid = authorityKeyIdentifier.RawData;
                            ValidateDer(encodedAkid);
                            writer.WriteOctetString(encodedAkid);
                        }

                        // CRL Number Extension
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier(TestOids.CrlNumber.Value!);

                            using (writer.PushOctetString())
                            {
                                writer.WriteInteger(crlNumber);
                            }
                        }
                    }
                }
            }

            byte[] tbsCertList = writer.Encode();
            writer.Reset();

            byte[] signature = generator.SignData(tbsCertList, hashAlgorithm);

            // CertificateList
            using (writer.PushSequence())
            {
                writer.WriteEncodedValue(tbsCertList);
                writer.WriteEncodedValue(signatureAlgId);
                writer.WriteBitString(signature);
            }

            byte[] crl = writer.Encode();
            return crl;
        }

        private static void ValidateDer(ReadOnlyMemory<byte> encodedValue)
        {
            try
            {
                Asn1Tag tag;
                AsnReader reader = new(encodedValue, AsnEncodingRules.DER);

                while (reader.HasData)
                {
                    tag = reader.PeekTag();

                    // If the tag is in the UNIVERSAL class
                    //
                    // DER limits the constructed encoding to SEQUENCE and SET, as well as anything which gets
                    // a defined encoding as being an IMPLICIT SEQUENCE.
                    if (tag.TagClass == TagClass.Universal)
                    {
                        switch ((UniversalTagNumber)tag.TagValue)
                        {
                            case UniversalTagNumber.External:
                            case UniversalTagNumber.Embedded:
                            case UniversalTagNumber.Sequence:
                            case UniversalTagNumber.Set:
                            case UniversalTagNumber.UnrestrictedCharacterString:
                                if (!tag.IsConstructed)
                                {
                                    throw new CryptographicException("ASN1 corrupted data.");
                                }

                                break;
                            default:
                                if (tag.IsConstructed)
                                {
                                    throw new CryptographicException("ASN1 corrupted data.");
                                }

                                break;
                        }
                    }

                    if (tag.IsConstructed)
                    {
                        ValidateDer(reader.PeekContentBytes());
                    }

                    // Skip past the current value.
                    reader.ReadEncodedValue();
                }
            }
            catch (AsnContentException e)
            {
                throw new CryptographicException("ASN1 corrupted data.", e);
            }
        }
    }
}

#endif
