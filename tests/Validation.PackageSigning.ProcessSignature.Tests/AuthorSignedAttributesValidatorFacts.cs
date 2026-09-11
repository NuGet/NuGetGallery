// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Formats.Asn1;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class AuthorSignedAttributesValidatorFacts
    {
        [Fact]
        public void AllowsMissingSignedAttributes()
        {
            using (X509Certificate2 certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
            {
                ContentInfo contentInfo = new(content: new byte[] { 0 });
                SignedCms signedCms = new(contentInfo);
                signedCms.ComputeSignature(new CmsSigner(certificate: certificate));
                byte[] signatureBytes = signedCms.Encode();
                SignedCms decodedSignedCms = new();
                decodedSignedCms.Decode(signatureBytes);

                Assert.Empty(decodedSignedCms.SignerInfos[0].SignedAttributes);
                Assert.True(AuthorSignedAttributesValidator.IsCanonical(signatureBytes));
            }
        }

        [Fact]
        public void ThrowsForTruncatedAsn1()
        {
            using (X509Certificate2 certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
            {
                ContentInfo contentInfo = new(content: new byte[] { 0 });
                SignedCms signedCms = new(contentInfo);
                signedCms.ComputeSignature(new CmsSigner(certificate: certificate));
                byte[] signatureBytes = signedCms.Encode();
                Array.Resize(ref signatureBytes, signatureBytes.Length - 1);

                Assert.Throws<AsnContentException>(
                    () => AuthorSignedAttributesValidator.IsCanonical(signatureBytes));
            }
        }

        [Fact]
        public void ThrowsDistinctExceptionForUnexpectedContentType()
        {
            AsnWriter writer = new(AsnEncodingRules.BER);
            writer.PushSequence();
            writer.WriteObjectIdentifier(oidValue: "1.2.3");
            writer.PopSequence();

            Assert.Throws<AuthorSignedAttributesValidator.UnexpectedCmsContentTypeException>(
                () => AuthorSignedAttributesValidator.IsCanonical(writer.Encode()));
        }
    }
}
