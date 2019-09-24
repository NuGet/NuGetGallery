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
        public void ConstructorThrowsWhenFeatureFlagServiceIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new ConfigurationIconFileProvider(
                    featureFlagService: null,
                    iconUrlTemplateProcessor: Mock.Of<IIconUrlTemplateProcessor>()));

            Assert.Equal("featureFlagService", ex.ParamName);
        }

        public void ConstructorThrowsWhenIconUrlTemplateProcessorIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new ConfigurationIconFileProvider(
                    featureFlagService: Mock.Of<IFeatureFlagService>(),
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
            protected Mock<IFeatureFlagService> _featureFlagServiceMock;
            protected Mock<IIconUrlTemplateProcessor> _iconUrlTemplateProcessorMock;

            public ConfigurationIconFileProviderFactsBase()
            {
                _featureFlagServiceMock = new Mock<IFeatureFlagService>();
                _iconUrlTemplateProcessorMock = new Mock<IIconUrlTemplateProcessor>();
                _iconUrlTemplateProcessorMock
                    .Setup(x => x.Process(It.IsAny<Package>()))
                    .Returns(DefaultIconUrlTemplateProcessorResult);
                _target = new ConfigurationIconFileProvider(_featureFlagServiceMock.Object, _iconUrlTemplateProcessorMock.Object);
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
            [InlineData(null, false, null, null)]
            [InlineData("", false, null, null)]
            [InlineData("https://example.test/icon", false, null, null)]
            [InlineData(null, false, "", null)]
            [InlineData("", false, "", null)]
            [InlineData("https://example.test/icon", false, "", null)]
            [InlineData(null, false, " ", null)]
            [InlineData("", false, " ", null)]
            [InlineData("https://example.test/icon", false, " ", null)]
            [InlineData(null, false, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("", false, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("https://example.test/icon", false, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData(null, true, null, null)]
            [InlineData("", true, null, null)]
            [InlineData("https://example.test/icon", true, null, null)]
            [InlineData(null, true, "", null)]
            [InlineData("", true, "", null)]
            [InlineData("https://example.test/icon", true, "", null)]
            [InlineData(null, true, " ", null)]
            [InlineData("", true, " ", null)]
            [InlineData("https://example.test/icon", true, " ", null)]
            [InlineData(null, true, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("", true, "https://storage.test/icon", "https://storage.test/icon")]
            [InlineData("https://example.test/icon", true, "https://storage.test/icon", "https://storage.test/icon")]
            public void AlwaysUsesEmbeddedIconUrlTemplateWhenPackageHasEmbeddedIcon(string iconUrl, bool forceFlatContainerIcons, string templateProcessorOutput, string expectedIconUrl)
            {
                _featureFlagServiceMock
                    .Setup(ffs => ffs.IsForceFlatContainerIconsEnabled())
                    .Returns(forceFlatContainerIcons);

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
            [InlineData(null, false, null, null)]
            [InlineData("", false, null, null)]
            [InlineData(" ", false, null, null)]
            [InlineData(null, false, "https://internal.test/icon", null)]
            [InlineData("", false, "https://internal.test/icon", null)]
            [InlineData(" ", false, "https://internal.test/icon", null)]
            [InlineData("https://external.test/icon", false, null, "https://external.test/icon")]
            [InlineData("https://external.test/icon", false, "https://internal.test/icon", "https://external.test/icon")]
            [InlineData(null, true, null, null)]
            [InlineData("", true, null, null)]
            [InlineData(" ", true, null, null)]
            [InlineData(null, true, "https://internal.test/icon", null)]
            [InlineData("", true, "https://internal.test/icon", null)]
            [InlineData(" ", true, "https://internal.test/icon", null)]
            [InlineData("https://external.test/icon", true, null, null)]
            [InlineData("https://external.test/icon", true, "https://internal.test/icon", "https://internal.test/icon")]
            public void ProducesExpectedIconUrlWhenNoEmbeddedIcon(string iconUrl, bool forceFlatContainerIcons, string templateProcessorOutput, string expectedIconUrl)
            {
                _featureFlagServiceMock
                    .Setup(ffs => ffs.IsForceFlatContainerIconsEnabled())
                    .Returns(forceFlatContainerIcons);

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
