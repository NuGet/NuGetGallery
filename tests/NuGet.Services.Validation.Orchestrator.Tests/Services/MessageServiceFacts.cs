// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGetGallery;
using NuGetGallery.Services;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class MessageServiceFacts
    {
        [Fact]
        public void ConstructorThrowsWhenCoreMessageServiceIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new PackageMessageService(null, EmailConfigurationAccessorMock.Object, LoggerMock.Object));
            Assert.Equal("coreMessageService", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenEmailConfigurationAccessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new PackageMessageService(CoreMessageServiceMock.Object, null, LoggerMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenLoggerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, null));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenEmailConfigurationValueIsNull()
        {
            EmailConfigurationAccessorMock
                .SetupGet(eca => eca.Value)
                .Returns((EmailConfiguration)null);

            var ex = Assert.Throws<ArgumentException>(() => new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
        }

        private static IEnumerable<string> InvalidValuesToTest => new string[] { null, "", " " };
        private const string ValidValue = "123";
        private const string ValidSettingsUrl = "https://example.com";
        public static IEnumerable<object[]> EmailConfigurationPropertyValuesCombinations =>
            (from invalidValue in InvalidValuesToTest
             select new[]
             {
                new object[] { invalidValue,   ValidValue, ValidSettingsUrl, "PackageUrlTemplate" },
                new object[] {   ValidValue, invalidValue, ValidSettingsUrl, "PackageSupportTemplate" },
                new object[] {   ValidValue,   ValidValue,     invalidValue, "EmailSettingsUrl" }
            }).SelectMany(x => x);

        [Theory]
        [MemberData(nameof(EmailConfigurationPropertyValuesCombinations))]
        public void ConstructorThrowsWhenEmailConfigurationPropertiesAreInvalid(
            string packageUrlTemplate,
            string packageSupportTemplate,
            string emailSettingsUrl,
            string expectedProperty)
        {
            EmailConfigurationAccessorMock
                .SetupGet(eca => eca.Value)
                .Returns(new EmailConfiguration
                {
                    PackageUrlTemplate = packageUrlTemplate,
                    PackageSupportTemplate = packageSupportTemplate,
                    EmailSettingsUrl = emailSettingsUrl
                });

            var ex = Assert.Throws<ArgumentException>(() => new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
            Assert.Contains(expectedProperty, ex.Message);
        }

        [Fact]
        public void ConstructorThrowsWhenEmailSettingsUrlIsNotProperUrl()
        {
            EmailConfigurationAccessorMock
                .SetupGet(eca => eca.Value)
                .Returns(new EmailConfiguration
                {
                    PackageUrlTemplate = "packageUrlTemplate",
                    PackageSupportTemplate = "packageSupportTemplate",
                    EmailSettingsUrl = "someRandomValue"
                });

            var ex = Assert.Throws<ArgumentException>(() => new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object));
            Assert.Equal("emailConfigurationAccessor", ex.ParamName);
            Assert.Contains("EmailSettingsUrl", ex.Message);
            Assert.Contains(" url", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SendPackagePublishedEmailMethodCallsCoreMessageService()
        {
            var expectedPackageUrl = string.Format(EmailConfiguration.PackageUrlTemplate, Package.PackageRegistration.Id, Package.NormalizedVersion);
            var expectedSupportUrl = string.Format(EmailConfiguration.PackageSupportTemplate, Package.PackageRegistration.Id, Package.NormalizedVersion);

            var service = new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object);

            var ex = Record.Exception(() => service.SendPublishedMessageAsync(Package).Wait());
            Assert.Null(ex);

            CoreMessageServiceMock
                .Verify(cms => cms.SendPackageAddedNoticeAsync(Package, expectedPackageUrl, expectedSupportUrl, ValidSettingsUrl, It.IsAny<IEnumerable<string>>()), Times.Once());
            CoreMessageServiceMock
                .Verify(cms => cms.SendPackageAddedNoticeAsync(It.IsAny<Package>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Once());
        }

        [Fact]
        public async Task SendPackagePublishedEmailThrowsWhenPackageIsNull()
        {
            var service = new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object);
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SendPublishedMessageAsync(null));
            Assert.Equal("package", ex.ParamName);
        }

        [Fact]
        public void SendPackageValidationFailedMessageCallsCoreMessageService()
        {
            var expectedPackageUrl = string.Format(EmailConfiguration.PackageUrlTemplate, Package.PackageRegistration.Id, Package.NormalizedVersion);
            var expectedSupportUrl = string.Format(EmailConfiguration.PackageSupportTemplate, Package.PackageRegistration.Id, Package.NormalizedVersion);

            var service = new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object);

            var ex = Record.Exception(() => service.SendValidationFailedMessageAsync(Package, ValidationSet).Wait());
            Assert.Null(ex);

            CoreMessageServiceMock
                .Verify(cms => cms.SendPackageValidationFailedNoticeAsync(Package, ValidationSet, expectedPackageUrl, expectedSupportUrl, EmailConfiguration.AnnouncementsUrl, EmailConfiguration.TwitterUrl), Times.Once());
            CoreMessageServiceMock
                .Verify(cms => cms.SendPackageValidationFailedNoticeAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task SendPackageValidationFailedMessageThrowsWhenPackageIsNull()
        {
            var service = new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object);
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SendValidationFailedMessageAsync(null, new PackageValidationSet()));
            Assert.Equal("package", ex.ParamName);
        }

        [Fact]
        public async Task SendPackageValidationFailedMessageThrowsWhenValidationSetIsNull()
        {
            var service = new PackageMessageService(CoreMessageServiceMock.Object, EmailConfigurationAccessorMock.Object, LoggerMock.Object);
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.SendValidationFailedMessageAsync(new Package(), null));
            Assert.Equal("validationSet", ex.ParamName);
        }

        public MessageServiceFacts()
        {
            EmailConfiguration = new EmailConfiguration
            {
                PackageUrlTemplate = "https://example.com/package/{0}/{1}",
                PackageSupportTemplate = "https://example.com/packageSupport/{0}/{1}",
                EmailSettingsUrl = ValidSettingsUrl,
                AnnouncementsUrl = "https://announcements.com",
                TwitterUrl = "https://twitter.com/nuget"
            };

            EmailConfigurationAccessorMock
                .SetupGet(eca => eca.Value)
                .Returns(EmailConfiguration);

            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package" },
                Version = "1.2.3.4",
                NormalizedVersion = "1.2.3"
            };
            Package.PackageRegistration.Packages = new List<Package> { Package };

            ValidationSet = new PackageValidationSet();
        }

        private Mock<ICoreMessageService> CoreMessageServiceMock { get; set; } = new Mock<ICoreMessageService>();
        private Mock<IOptionsSnapshot<EmailConfiguration>> EmailConfigurationAccessorMock { get; set; } = new Mock<IOptionsSnapshot<EmailConfiguration>>();
        private Mock<ILogger<PackageMessageService>> LoggerMock { get; set; } = new Mock<ILogger<PackageMessageService>>();
        private Package Package { get; set; }
        private PackageValidationSet ValidationSet { get; set; }
        private EmailConfiguration EmailConfiguration { get; set; }
    }
}
