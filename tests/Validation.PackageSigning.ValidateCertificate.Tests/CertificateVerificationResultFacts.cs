// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.Validation;
using Xunit;

namespace Validation.PackageSigning.ValidateCertificate.Tests
{
    public class CertificateVerificationResultFacts
    {
        [Fact]
        public void CannotCreateGoodResultWithErrors()
        {
            var exception = Assert.Throws<ArgumentException>(
                                () => CreateResult()
                                        .WithStatus(EndCertificateStatus.Good)
                                        .WithStatusFlags(X509ChainStatusFlags.ExplicitDistrust)
                                        .Build());

            Assert.StartsWith("Invalid flags 'ExplicitDistrust' for status 'Good'", exception.Message);
        }

        [Fact]
        public void CanCreateGoodResultWithNoStatusUpdateTime()
        {
            var result = CreateResult()
                            .WithStatus(EndCertificateStatus.Good)
                            .WithStatusFlags(X509ChainStatusFlags.NoError)
                            .Build();

            Assert.Equal(EndCertificateStatus.Good, result.Status);
        }

        [Fact]
        public void CannotCreateInvalidResultWithNoErrors()
        {
            var exception = Assert.Throws<ArgumentException>(
                                () => CreateResult()
                                        .WithStatus(EndCertificateStatus.Invalid)
                                        .WithStatusFlags(X509ChainStatusFlags.NoError)
                                        .Build());

            Assert.StartsWith("Invalid flags 'NoError' for status 'Invalid'", exception.Message);
        }

        [Fact]
        public void CanCreateInvalidResultWithoutStatusUpdateTime()
        {
            var result = CreateResult()
                            .WithStatus(EndCertificateStatus.Invalid)
                            .WithStatusFlags(X509ChainStatusFlags.ExplicitDistrust)
                            .WithStatusUpdateTime(null)
                            .Build();

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
        }

        [Fact]
        public void CanCreateInvalidResultWithStatusUpdateTime()
        {
            var result = CreateResult()
                .WithStatus(EndCertificateStatus.Invalid)
                .WithStatusFlags(X509ChainStatusFlags.ExplicitDistrust)
                .WithStatusUpdateTime(DateTime.UtcNow)
                .Build();

            Assert.Equal(EndCertificateStatus.Invalid, result.Status);
            Assert.NotNull(result.StatusUpdateTime);
        }

        [Theory]
        [InlineData(EndCertificateStatus.Good, X509ChainStatusFlags.NoError)]
        [InlineData(EndCertificateStatus.Unknown, X509ChainStatusFlags.OfflineRevocation)]
        public void CannotCreateNonRevokedResultWithRevocationDate(EndCertificateStatus status, X509ChainStatusFlags flags)
        {
            var revocationTime = new DateTime(2000, 1, 2);
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                return CreateResult()
                    .WithStatus(status)
                    .WithStatusFlags(flags)
                    .WithRevocationTime(revocationTime)
                    .Build();
            });

            Assert.StartsWith($"End certificate revoked at {revocationTime.ToShortDateString()}", exception.Message);
            Assert.Contains($"status is {status}", exception.Message);
        }

        [Fact]
        public void CannotCreateRevokedResultWithNoRevocationError()
        {
            var exception = Assert.Throws<ArgumentException>(
                    () => CreateResult()
                            .WithStatus(EndCertificateStatus.Revoked)
                            .WithStatusFlags(X509ChainStatusFlags.NoError)
                            .Build());

            Assert.StartsWith("Invalid flags 'NoError' for status 'Revoked'", exception.Message);
        }

        [Fact]
        public void CanCreateRevokedResultWithNoRevocationTime()
        {
            var exception = Assert.Throws<ArgumentException>(
                    () => CreateResult()
                            .WithStatus(EndCertificateStatus.Revoked)
                            .WithStatusFlags(X509ChainStatusFlags.NoError)
                            .WithRevocationTime(null)
                            .Build());

            Assert.StartsWith("Invalid flags 'NoError' for status 'Revoked'", exception.Message);
        }

        [Fact]
        public void CannotCreateUnknownResultWithoutOfflineErrors()
        {
            var exception = Assert.Throws<ArgumentException>(
                    () => CreateResult()
                            .WithStatus(EndCertificateStatus.Unknown)
                            .WithStatusFlags(X509ChainStatusFlags.ExplicitDistrust)
                            .WithRevocationTime(null)
                            .Build());

            Assert.StartsWith("Invalid flags 'ExplicitDistrust' for status 'Unknown'", exception.Message);
        }

        private static CertificateVerificationResult.Builder CreateResult() => new CertificateVerificationResult.Builder();
    }
}
