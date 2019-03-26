// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageTranslatorFacts
    {
        private static PackageTranslator CreatePackageTranslator()
        {
            return new TestablePackageTranslator();
        }

        [Fact]
        public void DoesNotThrowWhenGivenNull()
        {
            var target = CreatePackageTranslator();
            var result = target.TryTranslatePackageDefinition(null);
            Assert.False(result);
        }

        [Fact]
        public void DoesNotThrowWhenGivenOnlyPackageId()
        {
            var definition = new PackageDefinition {PackageId = "Foo"};

            var target = CreatePackageTranslator();
            var result = target.TryTranslatePackageDefinition(definition);

            Assert.False(result);
        }

        [Fact]
        public void DoesNotThrowWhenGivenOnlyPackageVersion()
        {
            var definition = new PackageDefinition {PackageVersion = "1.0"};

            var target = CreatePackageTranslator();
            var result = target.TryTranslatePackageDefinition(definition);

            Assert.False(result);
        }

        [Theory]
        [InlineData("donottranslate", "0.1.0", "donottranslate", "0.1.0")]
        [InlineData("package4", "0.1.0", "package4.0", "1.0")]
        [InlineData("package4", "0.2.3.5", "package4.0", "2.3.5")]
        [InlineData("package4", "5.1.0", "package4.5", "1.0")]
        [InlineData("package4", "5.2.3.5", "package4.5", "2.3.5")]
        public void CorrectlyTranslatesPackageDefinition(string a, string b, string c, string d)
        {
            var definition = new PackageDefinition
            {
                PackageId = a,
                PackageVersion = b
            };

            var target = CreatePackageTranslator();
            var result = target.TryTranslatePackageDefinition(definition);

            Assert.Equal(definition.PackageId, c);
            Assert.Equal(definition.PackageVersion, d);
        }
    }
}