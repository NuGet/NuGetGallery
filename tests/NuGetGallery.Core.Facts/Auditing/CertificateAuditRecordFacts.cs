// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class CertificateAuditRecordFacts
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_WhenThumbprintIsInvalid_Throws(string thumbprint)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CertificateAuditRecord(AuditedCertificateAction.Activate, thumbprint));

            Assert.Equal("thumbprint", exception.ParamName);
            Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
        }

        [Fact]
        public void Constructor_WhenThumbprintIsUnexpectedLength_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CertificateAuditRecord(AuditedCertificateAction.Activate, thumbprint: "a"));

            Assert.Equal("thumbprint", exception.ParamName);
            Assert.StartsWith("The thumbprint is expected to be a SHA-256 thumbprint, which is exactly 64 characters in length.  Did the hash algorithm change?", exception.Message);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            const string thumbprint = "ab53695a51124faada4ca40d776f6ca59afdfa37506df9f5e02782545373f727";

            var record = new CertificateAuditRecord(AuditedCertificateAction.Activate, thumbprint);

            Assert.Equal(AuditedCertificateAction.Activate, record.Action);
            Assert.Equal(thumbprint, record.Thumbprint);
            Assert.Equal("SHA-256", record.HashAlgorithm);
        }

        [Theory]
        [InlineData("ab53695a51124faada4ca40d776f6ca59afdfa37506df9f5e02782545373f727")]
        [InlineData("AB53695A51124FAADA4CA40D776F6CA59AFDFA37506DF9F5E02782545373F727")]
        public void GetPath_ReturnsLowercasedThumbprint(string thumbprint)
        {
            var record = new CertificateAuditRecord(AuditedCertificateAction.Add, thumbprint);

            Assert.Equal(thumbprint.ToLowerInvariant(), record.GetPath());
        }
    }
}