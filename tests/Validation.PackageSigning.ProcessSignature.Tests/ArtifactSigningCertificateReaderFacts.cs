// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class ArtifactSigningCertificateReaderFacts
    {
        public class TheTryGetDurableIdentityValueMethod
        {
            private const string ArtifactSigningEku = "1.3.6.1.4.1.311.97.1.0";
            private const string Div = "1.3.6.1.4.1.311.97.990309390.766961637.194916062.941502583";
            private const string CodeSigningEku = "1.3.6.1.5.5.7.3.3";

            private readonly X509Certificate2 _root = CreateCertificate("Test Root");
            private readonly X509Certificate2 _pca = CreateCertificate("Test PCA");
            private readonly X509Certificate2 _issuingCa = CreateCertificate("Test Issuing CA");
            private readonly ProcessSignatureConfiguration _configuration;
            private readonly ArtifactSigningCertificateReader _target;

            public TheTryGetDurableIdentityValueMethod()
            {
                _configuration = new ProcessSignatureConfiguration();
                _configuration.ArtifactSigning.PolicyCertificateThumbprints = new List<string> { _pca.ComputeSHA256Thumbprint() };
                var options = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
                options.Setup(x => x.Value).Returns(() => _configuration);
                _target = new ArtifactSigningCertificateReader(options.Object, NullLogger<ArtifactSigningCertificateReader>.Instance);
            }

            [Fact]
            public void WhenArtifactSigningCertificateChainsToPolicyCertificate_ReturnsDurableIdentityValue()
            {
                var leaf = CreateCertificate("Leaf", CodeSigningEku, ArtifactSigningEku, Div);

                var found = _target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _pca, _root }, out var value);

                Assert.True(found);
                Assert.Equal(Div, value);
            }

            [Fact]
            public void WhenExtraIntermediateCertificateIsPresent_ReturnsDurableIdentityValue()
            {
                var leaf = CreateCertificate("Leaf", ArtifactSigningEku, Div);
                var extraIntermediate = CreateCertificate("Extra Intermediate");

                var found = _target.TryGetDurableIdentityValue(new[] { leaf, extraIntermediate, _issuingCa, _pca, _root }, out var value);

                Assert.True(found);
                Assert.Equal(Div, value);
            }

            [Fact]
            public void WhenArtifactSigningEkuIsMissing_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", CodeSigningEku, Div);

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _pca, _root }, out var value));
                Assert.Null(value);
            }

            [Fact]
            public void WhenDurableIdentityValueEkuIsMissing_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", CodeSigningEku, ArtifactSigningEku);

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _pca, _root }, out var value));
                Assert.Null(value);
            }

            [Fact]
            public void WhenTwoDurableIdentityValueEkusArePresent_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", ArtifactSigningEku, Div, "1.3.6.1.4.1.311.97.1.2.3.4");

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _pca, _root }, out var value));
                Assert.Null(value);
            }

            [Fact]
            public void WhenChainDoesNotContainPolicyCertificate_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", ArtifactSigningEku, Div);

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _root }, out var value));
                Assert.Null(value);
            }

            [Fact]
            public void WhenOnlyLeafMatchesPolicyThumbprint_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", ArtifactSigningEku, Div);
                _configuration.ArtifactSigning.PolicyCertificateThumbprints = new List<string> { leaf.ComputeSHA256Thumbprint() };

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf, _issuingCa, _root }, out var value));
                Assert.Null(value);
            }

            [Fact]
            public void WhenPolicyThumbprintsConfigured_DefaultIsNotAccepted()
            {
                Assert.Equal(new[] { _pca.ComputeSHA256Thumbprint() }, _configuration.ArtifactSigning.GetPolicyCertificateThumbprints());
                Assert.DoesNotContain(
                    ArtifactSigningConfiguration.DefaultPolicyCertificateThumbprint,
                    _configuration.ArtifactSigning.GetPolicyCertificateThumbprints());
            }

            [Fact]
            public void WhenPolicyThumbprintsNotConfigured_UsesDefault()
            {
                var configuration = new ArtifactSigningConfiguration();

                Assert.Equal(
                    new[] { ArtifactSigningConfiguration.DefaultPolicyCertificateThumbprint },
                    configuration.GetPolicyCertificateThumbprints());
            }

            [Fact]
            public void WhenChainHasOnlyLeaf_ReturnsFalse()
            {
                var leaf = CreateCertificate("Leaf", ArtifactSigningEku, Div);

                Assert.False(_target.TryGetDurableIdentityValue(new[] { leaf }, out var value));
                Assert.Null(value);
            }

            private static X509Certificate2 CreateCertificate(string commonName, params string[] ekuOids)
            {
                using (var rsa = new RSACng(2048))
                {
                    var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                    if (ekuOids.Length > 0)
                    {
                        var oids = new OidCollection();
                        foreach (var oid in ekuOids)
                        {
                            oids.Add(new Oid(oid));
                        }

                        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oids, critical: false));
                    }

                    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
                }
            }
        }
    }
}
