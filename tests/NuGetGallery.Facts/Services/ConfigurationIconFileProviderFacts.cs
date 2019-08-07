// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using CommonMark.Syntax;
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
            [InlineData(null, null, null)]
            [InlineData("", null, null)]
            [InlineData("https://example.test/icon", null, null)]
            [InlineData(null, "", null)]
            [InlineData("", "", null)]
            [InlineData("https://example.test/icon", "", null)]
            [InlineData(null, " ", null)]
            [InlineData("", " ", null)]
            [InlineData("https://example.test/icon", " ", null)]
            [InlineData(null, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("", "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("https://example.test/icon", "https://storage.test/icon", "https://storage.test/icon")]
            public void AlwaysUsesEmbeddedIconUrlTemplateWhenPackageHasEmbeddedIcon(string iconUrl, string templateProcessorOutput, string expectedIconUrl)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "someId",
                    },
                    NormalizedVersion = "1.2.3",
                    IconUrl = iconUrl,
                    HasEmbeddedIcon = true,
                };

                _iconUrlTemplateProcessorMock
                    .Setup(x => x.Process(package))
                    .Returns(templateProcessorOutput);

                var producedIconUrl = CallTarget(package);

                _iconUrlTemplateProcessorMock
                    .Verify(x => x.Process(package), Times.Once);
                _iconUrlTemplateProcessorMock
                    .Verify(x => x.Process(It.IsAny<Package>()), Times.Once);

                Assert.Equal(expectedIconUrl, producedIconUrl);
            }

            [Theory]
            [InlineData(null, null, null)]
            [InlineData("", null, null)]
            [InlineData(" ", null, null)]
            [InlineData(null, "https://internal.test/icon", null)]
            [InlineData("", "https://internal.test/icon", null)]
            [InlineData(" ", "https://internal.test/icon", null)]
            [InlineData("https://external.test/icon", null, "https://external.test/icon")]
            [InlineData("https://external.test/icon", "https://internal.test/icon", "https://external.test/icon")]
            public void ProducesExpectedIconUrlWhenNoEmbeddedIcon(string iconUrl, string templateProcessorOutput, string expectedIconUrl)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "someId",
                    },
                    NormalizedVersion = "1.2.3",
                    IconUrl = iconUrl,
                    HasEmbeddedIcon = false,
                };

                _iconUrlTemplateProcessorMock
                    .Setup(x => x.Process(package))
                    .Returns(templateProcessorOutput);

                var producedIconUrl = CallTarget(package);

                Assert.Equal(expectedIconUrl, producedIconUrl);
            }
        }
    }
}
