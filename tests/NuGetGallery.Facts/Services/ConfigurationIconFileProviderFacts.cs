// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class ConfigurationIconFileProviderFacts
    {
        [Fact]
        public void ConstructorThrowsWhenConfigurationIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new ConfigurationIconFileProvider(
                    configuration: null,
                    iconUrlTemplateProcessor: Mock.Of<IIconUrlTemplateProcessor>()));

            Assert.Equal("configuration", ex.ParamName);
        }

        public void ConstructorThrowsWhenIconUrlTemplateProcessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new ConfigurationIconFileProvider(
                    configuration: Mock.Of<IAppConfiguration>(),
                    iconUrlTemplateProcessor: null));

            Assert.Equal("iconUrlTemplateProcessor", ex.ParamName);
        }

        public class TheGetIconUrlStringMethod : TheGetIconUrlMethodsBase
        {
            public override string CallTarget(Package package)
                => _target.GetIconUrlString(package);
        }

        public class TheGetIconUrlMethod : TheGetIconUrlMethodsBase
        {
            public override string CallTarget(Package package)
                => _target.GetIconUrl(package)?.AbsoluteUri;
        }

        public class ConfigurationIconFileProviderFactsBase
        {
            protected const string DefaultIconUrlTemplateProcessorResult = "https://processor.call.expected.test/";
            protected ConfigurationIconFileProvider _target;
            protected AppConfiguration _configuration;
            protected Mock<IIconUrlTemplateProcessor> _iconUrlTemplateProcessorMock;

            public ConfigurationIconFileProviderFactsBase()
            {
                _configuration = new AppConfiguration();
                _iconUrlTemplateProcessorMock = new Mock<IIconUrlTemplateProcessor>();
                _iconUrlTemplateProcessorMock
                    .Setup(x => x.Process(It.IsAny<Package>()))
                    .Returns(DefaultIconUrlTemplateProcessorResult);
                _target = new ConfigurationIconFileProvider(_configuration, _iconUrlTemplateProcessorMock.Object);
            }
        }

        public abstract class TheGetIconUrlMethodsBase : ConfigurationIconFileProviderFactsBase
        {
            public abstract string CallTarget(Package package);

            [Fact]
            public void ThrowsWhenPackageIsNull()
            {
                var ex = Assert.Throws<ArgumentNullException>(() => CallTarget(package: null));
                Assert.Equal("package", ex.ParamName);
            }

            [Theory]
            [InlineData(null, false, "SomeId", "1.2.3", null, true, null)]
            [InlineData(null, false, "SomeId", "1.2.3", "", true, null)]
            [InlineData(null, false, "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("", false, "SomeId", "1.2.3", null, true, null)]
            [InlineData("", false, "SomeId", "1.2.3", "", true, null)]
            [InlineData("", false, "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", null, true, DefaultIconUrlTemplateProcessorResult)]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", "", true, DefaultIconUrlTemplateProcessorResult)]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", "https://external.test/icon", true, DefaultIconUrlTemplateProcessorResult)]

            [InlineData(null, true, "SomeId", "1.2.3", null, true, null)]
            [InlineData(null, true, "SomeId", "1.2.3", "", true, null)]
            [InlineData(null, true, "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("", true, "SomeId", "1.2.3", null, true, null)]
            [InlineData("", true, "SomeId", "1.2.3", "", true, null)]
            [InlineData("", true, "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", null, true, DefaultIconUrlTemplateProcessorResult)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", "", true, DefaultIconUrlTemplateProcessorResult)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", "https://external.test/icon", true, DefaultIconUrlTemplateProcessorResult)]

            [InlineData(null, false, "SomeId", "1.2.3", null, false, null)]
            [InlineData(null, false, "SomeId", "1.2.3", "", false, null)]
            [InlineData(null, false, "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]
            [InlineData("", false, "SomeId", "1.2.3", null, false, null)]
            [InlineData("", false, "SomeId", "1.2.3", "", false, null)]
            [InlineData("", false, "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", null, false, null)]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", "", false, null)]
            [InlineData("https://internal.test/teststorage", false, "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]

            [InlineData(null, true, "SomeId", "1.2.3", null, false, null)]
            [InlineData(null, true, "SomeId", "1.2.3", "", false, null)]
            [InlineData(null, true, "SomeId", "1.2.3", "https://external.test/icon", false, null)]
            [InlineData("", true, "SomeId", "1.2.3", null, false, null)]
            [InlineData("", true, "SomeId", "1.2.3", "", false, null)]
            [InlineData("", true, "SomeId", "1.2.3", "https://external.test/icon", false, null)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", null, false, null)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", "", false, null)]
            [InlineData("https://internal.test/teststorage", true, "SomeId", "1.2.3", "https://external.test/icon", false, null)]
            public void ProducesExpectedIconUrl(string baseUrl, bool ignoreIconUrl, string id, string normalizedVersion, string iconUrl, bool hasEmbeddedIcon, string expectedIconUrl)
            {
                _configuration.EmbeddedIconUrlTemplate = baseUrl;
                _configuration.IgnoreIconUrl = ignoreIconUrl;

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = id,
                    },
                    NormalizedVersion = normalizedVersion,
                    IconUrl = iconUrl,
                    HasEmbeddedIcon = hasEmbeddedIcon,
                };

                var producedIconUrl = CallTarget(package);

                Assert.Equal(expectedIconUrl, producedIconUrl);
            }
        }
    }
}
