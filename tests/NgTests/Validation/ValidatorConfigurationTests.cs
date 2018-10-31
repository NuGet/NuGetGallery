// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests.Validation
{
    public class ValidatorConfigurationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_WhenPackageBaseAddressIsNullOrEmpty_Throws(string packageBaseAddress)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new ValidatorConfiguration(packageBaseAddress, requirePackageSignature: true));

            Assert.Equal("packageBaseAddress", exception.ParamName);
        }

        [Theory]
        [InlineData("a", true)]
        [InlineData("b", false)]
        public void Constructor_WhenArgumentsAreValid_InitializesInstance(
            string packageBaseAddress,
            bool requirePackageSignature)
        {
            var configuration = new ValidatorConfiguration(packageBaseAddress, requirePackageSignature);

            Assert.Equal(packageBaseAddress, configuration.PackageBaseAddress);
            Assert.Equal(requirePackageSignature, configuration.RequirePackageSignature);
        }
    }
}