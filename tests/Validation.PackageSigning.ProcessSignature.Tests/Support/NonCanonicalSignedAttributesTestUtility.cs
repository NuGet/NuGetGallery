// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using TestUtil;
using Xunit.Abstractions;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    internal static class NonCanonicalSignedAttributesTestUtility
    {
        private static readonly Asn1Tag ContextSpecific0 = new(tagClass: TagClass.ContextSpecific, tagValue: 0);
        private static readonly Asn1Tag ContextSpecific1 = new(tagClass: TagClass.ContextSpecific, tagValue: 1);

        public static MemoryStream CreatePackage(string packageId, string packageVersion)
        {
            PackageBuilder packageBuilder = new()
            {
                Id = packageId,
                Version = NuGetVersion.Parse(packageVersion),
                Description = "A package generated at runtime for signature validation tests.",
            };
            packageBuilder.Authors.Add("Test");
            packageBuilder.DependencyGroups.Add(
                new PackageDependencyGroup(
                    NuGetFramework.AnyFramework,
                    new[] { new PackageDependency(id: "RuntimeDependency", versionRange: VersionRange.All) }));

            MemoryStream packageStream = new();
            packageBuilder.Save(packageStream);
            packageStream.Position = 0;

            return packageStream;
        }

        public static async Task<byte[]> CreateAuthorSignedPackageAsync(
            Stream packageStream,
            X509Certificate2 certificate,
            ITestOutputHelper output)
        {
            X509SignatureProvider signatureProvider = new(timestampProvider: null);

            using (MemoryStream outputPackageStream = new())
            {
                await SigningUtility.SignAsync(
                    new SigningOptions(
                        inputPackageStream: new Lazy<Stream>(() => packageStream),
                        outputPackageStream: new Lazy<Stream>(() => outputPackageStream),
                        overwrite: true,
                        signatureProvider: signatureProvider,
                        logger: new TestLogger(output)),
                    new AuthorSignPackageRequest(certificate, NuGet.Common.HashAlgorithmName.SHA256),
                    CancellationToken.None);

                return outputPackageStream.ToArray();
            }
        }

        public static NonCanonicalSignatureResult MakeSignedAttributesNonCanonical(
            byte[] packageBytes,
            X509Certificate2 certificate)
        {
            byte[] signatureBytes = GetSignatureBytes(packageBytes);

            SignerInfoParts signerInfoParts = ReadSignerInfoParts(signatureBytes);
            List<byte[]> attributes = signerInfoParts.SignedAttributes.ToList();
            int swapIndex = Enumerable.Range(start: 0, count: attributes.Count - 1)
                .First(index => Compare(attributes[index], attributes[index + 1]) < 0);
            byte[][] reorderedAttributes = attributes.ToArray();
            byte[] temporary = reorderedAttributes[swapIndex];
            reorderedAttributes[swapIndex] = reorderedAttributes[swapIndex + 1];
            reorderedAttributes[swapIndex + 1] = temporary;

            byte[] nonCanonicalSignedAttributes = (byte[])signerInfoParts.EncodedSignedAttributes.Clone();
            int contentOffset = nonCanonicalSignedAttributes.Length - attributes.Sum(attribute => attribute.Length);
            int writeOffset = contentOffset;

            foreach (byte[] attribute in reorderedAttributes)
            {
                Buffer.BlockCopy(
                    src: attribute,
                    srcOffset: 0,
                    dst: nonCanonicalSignedAttributes,
                    dstOffset: writeOffset,
                    count: attribute.Length);
                writeOffset += attribute.Length;
            }

            // SignerInfo.signedAttrs is encoded on the wire with its [0] IMPLICIT tag. RFC 5652
            // requires the signature input to use the same contents with the universal SET OF tag.
            byte[] canonicalSignatureInput = (byte[])signerInfoParts.EncodedSignedAttributes.Clone();
            canonicalSignatureInput[0] = 0x31;
            byte[] nonCanonicalSignatureInput = (byte[])nonCanonicalSignedAttributes.Clone();
            nonCanonicalSignatureInput[0] = 0x31;

            byte[] signatureValue;
            using (RSA privateKey = certificate.GetRSAPrivateKey())
            {
                signatureValue = privateKey.SignData(
                    nonCanonicalSignatureInput,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
            }

            byte[] encodedSignatureValue = (byte[])signerInfoParts.EncodedSignatureValue.Clone();
            int signatureValueOffset = encodedSignatureValue.Length - signerInfoParts.SignatureValue.Length;
            Buffer.BlockCopy(
                src: signatureValue,
                srcOffset: 0,
                dst: encodedSignatureValue,
                dstOffset: signatureValueOffset,
                count: signatureValue.Length);

            int signedAttributesOffset = FindUniqueOffset(signatureBytes, signerInfoParts.EncodedSignedAttributes);
            int signatureValueFieldOffset = FindUniqueOffset(signatureBytes, signerInfoParts.EncodedSignatureValue);
            Buffer.BlockCopy(
                src: nonCanonicalSignedAttributes,
                srcOffset: 0,
                dst: signatureBytes,
                dstOffset: signedAttributesOffset,
                count: nonCanonicalSignedAttributes.Length);
            Buffer.BlockCopy(
                src: encodedSignatureValue,
                srcOffset: 0,
                dst: signatureBytes,
                dstOffset: signatureValueFieldOffset,
                count: encodedSignatureValue.Length);

            MemoryStream outputPackageStream = new(packageBytes);
            ReplaceSignature(outputPackageStream, signatureBytes);

            return new NonCanonicalSignatureResult(
                outputPackageStream,
                canonicalSignatureInput,
                nonCanonicalSignatureInput,
                signatureValue);
        }

        public static byte[] GetSignatureBytes(byte[] packageBytes)
        {
            using (MemoryStream packageStream = new(packageBytes))
            using (ZipArchive archive = new(packageStream, ZipArchiveMode.Read))
            using (Stream signatureStream = archive.GetEntry(SigningSpecifications.V1.SignaturePath).Open())
            using (MemoryStream memoryStream = new())
            {
                signatureStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private static SignerInfoParts ReadSignerInfoParts(byte[] signatureBytes)
        {
            // ContentInfo ::= SEQUENCE { contentType, [0] EXPLICIT content }
            AsnReader contentInfo = new AsnReader(signatureBytes, AsnEncodingRules.BER).ReadSequence();
            contentInfo.ReadObjectIdentifier();

            // ContentInfo.content contains the SignedData SEQUENCE.
            AsnReader signedDataContent = contentInfo.ReadSequence(ContextSpecific0);
            AsnReader signedData = signedDataContent.ReadSequence();

            // SignedData.version, digestAlgorithms, and encapContentInfo.
            signedData.ReadInteger();
            signedData.ReadEncodedValue();
            signedData.ReadEncodedValue();

            // SignedData optionally contains certificates [0] and CRLs [1] before signerInfos.
            while (signedData.HasData
                && (signedData.PeekTag().HasSameClassAndValue(ContextSpecific0)
                    || signedData.PeekTag().HasSameClassAndValue(ContextSpecific1)))
            {
                signedData.ReadEncodedValue();
            }

            // SignedData.signerInfos is a SET OF; NuGet package signatures have one primary SignerInfo.
            AsnReader signerInfo = signedData.ReadSetOf(skipSortOrderValidation: true).ReadSequence();

            // SignerInfo.version, sid, and digestAlgorithm precede signedAttrs.
            signerInfo.ReadInteger();
            signerInfo.ReadEncodedValue();
            signerInfo.ReadEncodedValue();

            // Preserve the complete [0] IMPLICIT signedAttrs field and each enclosed Attribute TLV.
            byte[] encodedSignedAttributes = signerInfo.ReadEncodedValue().ToArray();
            AsnReader signedAttributes = new AsnReader(
                encodedSignedAttributes,
                AsnEncodingRules.BER).ReadSetOf(
                    skipSortOrderValidation: true,
                    expectedTag: ContextSpecific0);
            List<byte[]> attributes = new();
            while (signedAttributes.HasData)
            {
                attributes.Add(signedAttributes.ReadEncodedValue().ToArray());
            }

            // SignerInfo.signatureAlgorithm is followed by the signature OCTET STRING.
            signerInfo.ReadEncodedValue();
            byte[] encodedSignatureValue = signerInfo.ReadEncodedValue().ToArray();
            byte[] signatureValue = new AsnReader(
                encodedSignatureValue,
                AsnEncodingRules.BER).ReadOctetString();

            return new SignerInfoParts(
                encodedSignedAttributes,
                attributes,
                encodedSignatureValue,
                signatureValue);
        }

        private static void ReplaceSignature(MemoryStream packageStream, byte[] signatureBytes)
        {
            using (ICSharpCode.SharpZipLib.Zip.ZipFile zipFile = new(packageStream))
            {
                zipFile.IsStreamOwner = false;
                zipFile.BeginUpdate();
                zipFile.Delete(SigningSpecifications.V1.SignaturePath);
                zipFile.CommitUpdate();
                zipFile.BeginUpdate();
                zipFile.Add(
                    new StreamDataSource(new MemoryStream(buffer: signatureBytes)),
                    SigningSpecifications.V1.SignaturePath,
                    CompressionMethod.Stored);
                zipFile.CommitUpdate();
            }

            packageStream.Position = 0;
        }

        private static int FindUniqueOffset(byte[] source, byte[] value)
        {
            int foundOffset = -1;

            for (int offset = 0; offset <= source.Length - value.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < value.Length; index++)
                {
                    if (source[offset + index] != value[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    if (foundOffset >= 0)
                    {
                        throw new InvalidOperationException("The encoded value was not unique in the CMS.");
                    }

                    foundOffset = offset;
                }
            }

            if (foundOffset < 0)
            {
                throw new InvalidOperationException("The encoded value was not found in the CMS.");
            }

            return foundOffset;
        }

        private static int Compare(byte[] left, byte[] right)
        {
            int commonLength = Math.Min(left.Length, right.Length);

            for (int index = 0; index < commonLength; index++)
            {
                int comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Length.CompareTo(right.Length);
        }

        internal sealed class NonCanonicalSignatureResult
        {
            public NonCanonicalSignatureResult(
                MemoryStream packageStream,
                byte[] canonicalSignatureInput,
                byte[] nonCanonicalSignatureInput,
                byte[] signatureValue)
            {
                PackageStream = packageStream;
                CanonicalSignatureInput = canonicalSignatureInput;
                NonCanonicalSignatureInput = nonCanonicalSignatureInput;
                SignatureValue = signatureValue;
            }

            public MemoryStream PackageStream { get; }
            public byte[] CanonicalSignatureInput { get; }
            public byte[] NonCanonicalSignatureInput { get; }
            public byte[] SignatureValue { get; }
        }

        private sealed class SignerInfoParts
        {
            public SignerInfoParts(
                byte[] encodedSignedAttributes,
                IReadOnlyList<byte[]> signedAttributes,
                byte[] encodedSignatureValue,
                byte[] signatureValue)
            {
                EncodedSignedAttributes = encodedSignedAttributes;
                SignedAttributes = signedAttributes;
                EncodedSignatureValue = encodedSignatureValue;
                SignatureValue = signatureValue;
            }

            public byte[] EncodedSignedAttributes { get; }
            public IReadOnlyList<byte[]> SignedAttributes { get; }
            public byte[] EncodedSignatureValue { get; }
            public byte[] SignatureValue { get; }
        }

        private sealed class StreamDataSource : IStaticDataSource
        {
            private readonly Stream _stream;

            public StreamDataSource(Stream stream)
            {
                _stream = stream;
            }

            public Stream GetSource()
            {
                return _stream;
            }
        }
    }
}
