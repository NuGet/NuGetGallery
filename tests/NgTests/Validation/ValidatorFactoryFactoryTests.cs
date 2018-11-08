// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests.Validation
{
    public class ValidatorFactoryFactoryTests
    {
        private static readonly ValidatorConfiguration _configuration = new ValidatorConfiguration(packageBaseAddress: "a", requirePackageSignature: true);

        [Fact]
        public void Constructor_WhenValidatorConfigIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidatorFactoryFactory(
                    validatorConfig: null,
                    loggerFactory: Mock.Of<ILoggerFactory>()));

            Assert.Equal("validatorConfig", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidatorFactoryFactory(
                    _configuration,
                    loggerFactory: null));

            Assert.Equal("loggerFactory", exception.ParamName);
        }

        [Fact]
        public void Create_WhenArgumentsAreValid_ReturnsValidatorFactory()
        {
            var factoryFactory = new ValidatorFactoryFactory(_configuration, Mock.Of<ILoggerFactory>());
            var factory = factoryFactory.Create("b", "c");

            Assert.NotNull(factory);
        }
    }
}