// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Jobs.Validation.Helper.Tests
{
    public class MarkCleanFacts
    {
        [Theory]
        [InlineData("Newtonsoft.Json", "Newtonsoft.Json")]
        [InlineData("%E8%8B%8F%E5%AE%81%E5%BC%80%E6%94%BE%E5%B9%B3%E5%8F%B0", "苏宁开放平台")]
        public void PackageIdUrlDecoded(string argument, string expectedPackageid)
        {
            MarkClean mc = CreateMarkClean(argument, "1.0.0");

            Assert.Equal(expectedPackageid, mc.PackageId);
        }

        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.0.0+msbuild", "1.0.0 msbuild")]
        [InlineData("1.0.0%2Bmsbuild", "1.0.0+msbuild")]
        public void PackageVersionUrlDecoded(string argument, string expectedPackageVersion)
        {
            MarkClean mc = CreateMarkClean("Newtonsoft.Json", argument);

            Assert.Equal(expectedPackageVersion, mc.PackageVersion);
        }

        private static MarkClean CreateMarkClean(string packageId, string packageVersion)
        {
            var loggerMock = new Mock<ILogger<MarkClean>>();
            var args = new Dictionary<string, string>
            {
                { CommandLineArguments.PackageId, packageId },
                { CommandLineArguments.PackageVersion, packageVersion },
                { CommandLineArguments.ValidationId, Guid.NewGuid().ToString() },
                { CommandLineArguments.Comment, "comment" },
                { CommandLineArguments.Alias, "angrigor" },
            };

            var mc = new MarkClean(args, loggerMock.Object, null, "container", null, null, "https://www.nuget.org/");
            return mc;
        }
    }
}
