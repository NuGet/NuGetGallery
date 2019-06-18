// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var ex = Assert.Throws<ArgumentNullException>(() => new ConfigurationIconFileProvider(configuration: null));

            Assert.Equal("configuration", ex.ParamName);
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
            protected ConfigurationIconFileProvider _target;
            protected AppConfiguration _configuration;

            public ConfigurationIconFileProviderFactsBase()
            {
                _configuration = new AppConfiguration();
                _target = new ConfigurationIconFileProvider(_configuration);
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
            [InlineData(null, "SomeId", "1.2.3", null, true, null)]
            [InlineData(null, "SomeId", "1.2.3", "", true, null)]
            [InlineData(null, "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("", "SomeId", "1.2.3", null, true, null)]
            [InlineData("", "SomeId", "1.2.3", "", true, null)]
            [InlineData("", "SomeId", "1.2.3", "https://external.test/icon", true, null)]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", null, true, "https://internal.test/teststorage/someid/1.2.3/icon")]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", "", true, "https://internal.test/teststorage/someid/1.2.3/icon")]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", "https://external.test/icon", true, "https://internal.test/teststorage/someid/1.2.3/icon")]
            [InlineData(null, "SomeId", "1.2.3", null, false, null)]
            [InlineData(null, "SomeId", "1.2.3", "", false, null)]
            [InlineData(null, "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]
            [InlineData("", "SomeId", "1.2.3", null, false, null)]
            [InlineData("", "SomeId", "1.2.3", "", false, null)]
            [InlineData("", "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", null, false, null)]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", "", false, null)]
            [InlineData("https://internal.test/teststorage", "SomeId", "1.2.3", "https://external.test/icon", false, "https://external.test/icon")]
            public void ProducesExpectedIconUrl(string baseUrl, string id, string normalizedVersion, string iconUrl, bool usesIconFromFlatContainer, string expectedIconUrl)
            {
                _configuration.InternalIconUrlBaseAddress = baseUrl;
                _configuration.IgnoreIconUrl = false;

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = id,
                    },
                    NormalizedVersion = normalizedVersion,
                    IconUrl = iconUrl,
                    UsesIconFromFlatContainer = usesIconFromFlatContainer,
                };

                var producedIconUrl = CallTarget(package);

                Assert.Equal(expectedIconUrl, producedIconUrl);
            }
        }
    }
}
