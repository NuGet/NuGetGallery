// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.Services
{
    public class IconUrlTemplateProcessorFacts
    {
        [Fact]
        public void ConstructorThrowsWhenConfigurationIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new IconUrlTemplateProcessor(configuration: null));
            Assert.Equal("configuration", ex.ParamName);
        }

        [Theory]
        [InlineData("https://nuget.test/icon", "someId", "someVersion", "https://nuget.test/icon")]
        [InlineData("https://nuget.test/{id-lower}/{version-lower}/icon", "someId", "someVersion", "https://nuget.test/someid/someversion/icon")]
        public void SubstitutesIdAndVersion(string template, string id, string version, string expectedUrl)
        {
            var configurationMock = new Mock<IAppConfiguration>();
            configurationMock
                .SetupGet(c => c.InternalIconUrlBaseAddress)
                .Returns(template);

            var package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = id },
                NormalizedVersion = version
            };

            var target = new IconUrlTemplateProcessor(configurationMock.Object);

            var result = target.Process(package);

            Assert.Equal(expectedUrl, result);
        }
    }
}
