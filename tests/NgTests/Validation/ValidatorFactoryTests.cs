// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Catalog;
using Xunit;

namespace NgTests.Validation
{
    public class ValidatorFactoryTests
    {
        private readonly ValidatorConfiguration _configuration;

        public ValidatorFactoryTests()
        {
            _configuration = new ValidatorConfiguration(packageBaseAddress: "a", requirePackageSignature: true);
        }

        [Fact]
        public void Constructor_WhenValidatorConfigIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidatorFactory(
                    validatorConfig: null,
                    loggerFactory: Mock.Of<ILoggerFactory>()));

            Assert.Equal("validatorConfig", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new ValidatorFactory(
                    _configuration,
                    loggerFactory: null));

            Assert.Equal("loggerFactory", exception.ParamName);
        }

        [Fact]
        public void Create_WhenValidatorTypeIsNull_Throws()
        {
            var factory = new ValidatorFactory(_configuration, Mock.Of<ILoggerFactory>());

            var exception = Assert.Throws<ArgumentNullException>(() => factory.Create(validatorType: null));

            Assert.Equal("validatorType", exception.ParamName);
        }

        [Fact]
        public void Create_WhenValidatorTypeLacksAppropriateConstructor_Throws()
        {
            var factory = new ValidatorFactory(_configuration, Mock.Of<ILoggerFactory>());

            var exception = Assert.Throws<Exception>(() => factory.Create(typeof(ValidatorFactoryTests)));

            Assert.Contains("Could not initialize", exception.Message);
        }

        [Fact]
        public void Create_WhenArgumentsAreValid_ReturnsValidatorFactory()
        {
            var factory = new ValidatorFactory(_configuration, Mock.Of<ILoggerFactory>());

            var validator = factory.Create(typeof(PackageHasSignatureValidator));

            Assert.IsType<PackageHasSignatureValidator>(validator);
        }
    }
}