// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class DurableIdentityValueServiceFacts
    {
        public class TheProcessAsyncMethod
        {
            private const string Div = "1.3.6.1.4.1.311.97.990309390.766961637.194916062.941502583";

            private readonly X509Certificate2 _signingCertificate;
            private readonly string _signingThumbprint;
            private readonly SignatureValidationMessage _message;
            private readonly PackageRegistration _packageRegistration;
            private readonly User _owner;
            private readonly User _otherOwner;
            private readonly Certificate _registeredCertificate;
            private readonly List<DurableIdentityValue> _durableIdentityValues = new List<DurableIdentityValue>();
            private readonly List<Certificate> _certificates = new List<Certificate>();
            private readonly Mock<IEntitiesContext> _entitiesContext;
            private readonly Mock<IFeatureFlagService> _featureFlagService;
            private readonly Mock<ITelemetryService> _telemetryService;
            private readonly ProcessSignatureConfiguration _configuration;
            private readonly DurableIdentityValueService _target;

            public TheProcessAsyncMethod()
            {
                using (var rsa = new RSACng(2048))
                {
                    var request = new CertificateRequest("CN=Contoso, O=Contoso", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    _signingCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
                }

                _signingThumbprint = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
                _message = new SignatureValidationMessage("Contoso.Package", "1.0.0", new Uri("https://unit.test/contoso.package.1.0.0.nupkg"), Guid.NewGuid());

                _owner = new User("owner") { Key = 1 };
                _otherOwner = new User("other") { Key = 2 };
                _packageRegistration = new PackageRegistration { Key = 3, Id = _message.PackageId };
                _packageRegistration.Owners.Add(_owner);
                _packageRegistration.Owners.Add(_otherOwner);

                _registeredCertificate = new Certificate { Key = 4, Thumbprint = _signingThumbprint };
                _certificates.Add(_registeredCertificate);

                _entitiesContext = new Mock<IEntitiesContext>();
                _entitiesContext.SetupDbSet(c => c.DurableIdentityValues, new Mock<DbSet<DurableIdentityValue>>(), _durableIdentityValues);
                _entitiesContext.SetupDbSet(c => c.Certificates, new Mock<DbSet<Certificate>>(), _certificates);

                _featureFlagService = new Mock<IFeatureFlagService>();
                _featureFlagService
                    .Setup(x => x.IsArtifactSigningDurableIdentityEnabled(It.IsAny<User>()))
                    .Returns(true);

                _telemetryService = new Mock<ITelemetryService>();

                _configuration = new ProcessSignatureConfiguration();
                var options = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
                options.Setup(x => x.Value).Returns(() => _configuration);

                _target = new DurableIdentityValueService(
                    _entitiesContext.Object,
                    _featureFlagService.Object,
                    options.Object,
                    _telemetryService.Object,
                    NullLogger<DurableIdentityValueService>.Instance);
            }

            [Fact]
            public async Task WhenOwnerHoldsCertificate_LinksDurableIdentityValueAndKeepsCertificateLink()
            {
                RegisterCertificate(_owner);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                var record = Assert.Single(_entitiesContext.Object.DurableIdentityValues);
                Assert.Equal(Div, record.Value);
                Assert.Equal(_signingCertificate.Subject, record.Subject);
                Assert.Equal(_signingCertificate.Issuer, record.Issuer);
                Assert.Equal("Contoso", record.ShortSubject);
                var link = Assert.Single(_owner.UserDurableIdentityValues);
                Assert.Same(record, link.DurableIdentityValue);
                Assert.Single(_owner.UserCertificates);
                Assert.Empty(_otherOwner.UserDurableIdentityValues);
                Assert.Same(record, _registeredCertificate.DurableIdentityValue);
                _entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
                _telemetryService.Verify(
                    x => x.TrackDurableIdentityValueLinked(_message.PackageId, _message.PackageVersion, _message.ValidationId, 1),
                    Times.Once);
            }

            [Fact]
            public async Task WhenFlightIsDisabled_DoesNotLink()
            {
                RegisterCertificate(_owner);
                _featureFlagService
                    .Setup(x => x.IsArtifactSigningDurableIdentityEnabled(_owner))
                    .Returns(false);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                Assert.Empty(_owner.UserDurableIdentityValues);
            }

            [Fact]
            public async Task WhenSignatureIsOlderThanLinkWindow_DoesNotLink()
            {
                RegisterCertificate(_owner);

                await ProcessAsync(DateTimeOffset.UtcNow - _configuration.ArtifactSigning.LinkWindow - TimeSpan.FromMinutes(1));

                Assert.Empty(_owner.UserDurableIdentityValues);
            }

            [Fact]
            public async Task WhenAccountDoesNotHoldCertificate_DoesNotLink()
            {
                RegisterCertificate(_owner);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                Assert.Empty(_otherOwner.UserDurableIdentityValues);
            }

            [Fact]
            public async Task WhenSeveralOwnersHoldCertificate_LinksEach()
            {
                RegisterCertificate(_owner);
                RegisterCertificate(_otherOwner);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                Assert.Single(_owner.UserDurableIdentityValues);
                Assert.Single(_otherOwner.UserDurableIdentityValues);
                _telemetryService.Verify(
                    x => x.TrackDurableIdentityValueLinked(_message.PackageId, _message.PackageVersion, _message.ValidationId, 2),
                    Times.Once);
            }

            [Fact]
            public async Task WhenRequiredSignerIsSet_LinksOnlyRequiredSigner()
            {
                RegisterCertificate(_owner);
                RegisterCertificate(_otherOwner);
                _packageRegistration.RequiredSigners.Add(_otherOwner);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                Assert.Empty(_owner.UserDurableIdentityValues);
                Assert.Single(_otherOwner.UserDurableIdentityValues);
            }

            [Fact]
            public async Task WhenCalledTwice_IsIdempotent()
            {
                RegisterCertificate(_owner);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));
                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                Assert.Single(_entitiesContext.Object.DurableIdentityValues);
                Assert.Single(_entitiesContext.Object.Certificates);
                Assert.Single(_owner.UserDurableIdentityValues);
            }

            [Fact]
            public async Task WhenDurableIdentityValueExists_UpdatesSubjectAndIssuer()
            {
                var existing = new DurableIdentityValue { Key = 9, Value = Div, Subject = "CN=Old", Issuer = "CN=Old Issuer" };
                _durableIdentityValues.Add(existing);

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                var record = Assert.Single(_entitiesContext.Object.DurableIdentityValues);
                Assert.Same(existing, record);
                Assert.Equal(_signingCertificate.Subject, record.Subject);
                Assert.Equal(_signingCertificate.Issuer, record.Issuer);
            }

            [Fact]
            public async Task WhenCertificateIsUnknown_CreatesCertificateLinkedToDurableIdentityValue()
            {
                _certificates.Clear();

                await ProcessAsync(DateTimeOffset.UtcNow.AddHours(-1));

                var certificate = Assert.Single(_entitiesContext.Object.Certificates);
                Assert.Equal(_signingThumbprint, certificate.Thumbprint);
#pragma warning disable CS0618 // Sha1Thumbprint is required by the schema.
                Assert.Equal(_signingCertificate.Thumbprint.ToLowerInvariant(), certificate.Sha1Thumbprint);
#pragma warning restore CS0618
                Assert.Same(Assert.Single(_entitiesContext.Object.DurableIdentityValues), certificate.DurableIdentityValue);
            }

            private Task ProcessAsync(DateTimeOffset signatureTimestamp)
            {
                return _target.ProcessAsync(
                    _message,
                    _packageRegistration,
                    _signingCertificate,
                    _signingThumbprint,
                    Div,
                    signatureTimestamp);
            }

            private void RegisterCertificate(User user)
            {
                var userCertificate = new UserCertificate
                {
                    Key = user.Key + 100,
                    CertificateKey = _registeredCertificate.Key,
                    Certificate = _registeredCertificate,
                    UserKey = user.Key,
                    User = user,
                };
                user.UserCertificates.Add(userCertificate);
                _registeredCertificate.UserCertificates.Add(userCertificate);
            }
        }
    }
}
