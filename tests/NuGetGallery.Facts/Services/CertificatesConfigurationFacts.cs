// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Services
{
    public class CertificatesConfigurationFacts
    {
        private readonly User _user;

        public CertificatesConfigurationFacts()
        {
            _user = new User()
            {
                Key = 1,
                Username = "a",
                EmailAddress = "a@nuget.test"
            };
        }

        [Fact]
        public void Constructor_WhenAlwaysEnabledForDomainsIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificatesConfiguration(
                    isUIEnabledByDefault: true,
                    alwaysEnabledForDomains: null,
                    alwaysEnabledForEmailAddresses: Enumerable.Empty<string>()));

            Assert.Equal("alwaysEnabledForDomains", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenAlwaysEnabledForEmailAddressesIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CertificatesConfiguration(
                    isUIEnabledByDefault: true,
                    alwaysEnabledForDomains: Enumerable.Empty<string>(),
                    alwaysEnabledForEmailAddresses: null));

            Assert.Equal("alwaysEnabledForEmailAddresses", exception.ParamName);
        }

        [Fact]
        public void Constructor_Default_InitializesProperties()
        {
            var configuration = new CertificatesConfiguration();

            Assert.False(configuration.IsUIEnabledByDefault);
            Assert.Empty(configuration.AlwaysEnabledForDomains);
            Assert.Empty(configuration.AlwaysEnabledForEmailAddresses);
        }

        [Fact]
        public void Constructor_NonDefault_InitializesProperties()
        {
            var domains = new[] { "a" };
            var emailAddresses = new[] { "b" };

            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: true,
                alwaysEnabledForDomains: domains,
                alwaysEnabledForEmailAddresses: emailAddresses);

            Assert.True(configuration.IsUIEnabledByDefault);
            Assert.Equal(domains, configuration.AlwaysEnabledForDomains);
            Assert.Equal(emailAddresses, configuration.AlwaysEnabledForEmailAddresses);
        }

        [Fact]
        public void IsUIEnabledForUser_WhenUserIsNull_ReturnsFalse()
        {
            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: true,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>());

            Assert.False(configuration.IsUIEnabledForUser(user: null));
        }

        [Fact]
        public void IsUIEnabledForUser_WithDefaults_ReturnsFalse()
        {
            var configuration = new CertificatesConfiguration();

            Assert.False(configuration.IsUIEnabledForUser(_user));
        }

        [Fact]
        public void IsUIEnabledForUser_WhenNotEnabled_ReturnsFalse()
        {
            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: false,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>());

            Assert.False(configuration.IsUIEnabledForUser(_user));
        }

        [Fact]
        public void IsUIEnabledForUser_WhenUIIsEnabledByDefault_ReturnsTrue()
        {
            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: true,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>());

            Assert.True(configuration.IsUIEnabledForUser(_user));
        }

        [Theory]
        [InlineData("nuget.test")]
        [InlineData("NUGET.TEST")]
        public void IsUIEnabledForUser_WhenUIIsEnabledForDomain_ReturnsTrue(string domain)
        {
            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: false,
                alwaysEnabledForDomains: new[] { domain },
                alwaysEnabledForEmailAddresses: Enumerable.Empty<string>());

            Assert.True(configuration.IsUIEnabledForUser(_user));
        }

        [Theory]
        [InlineData("a@nuget.test")]
        [InlineData("A@NUGET.TEST")]
        public void IsUIEnabledForUser_WhenUIIsEnabledForEmailAddress_ReturnsTrue(string emailAddress)
        {
            var configuration = new CertificatesConfiguration(
                isUIEnabledByDefault: false,
                alwaysEnabledForDomains: Enumerable.Empty<string>(),
                alwaysEnabledForEmailAddresses: new[] { emailAddress });

            Assert.True(configuration.IsUIEnabledForUser(_user));
        }
    }
}