// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationProviderFacts
    {
        private Mock<IServiceProvider> ServiceProviderMock { get; }
        private Mock<ILogger<ValidatorProvider>> LoggerMock { get; }

        [Fact]
        public void ValidatorSmokeTest()
        {
            var validator = new TestValidator();

            ServiceProviderMock
                .Setup(sp => sp.GetService(validator.GetType()))
                .Returns(() => validator);

            var provider = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            var result = provider.GetValidator(validator.GetType().Name);

            ServiceProviderMock
                .Verify(sp => sp.GetService(validator.GetType()), Times.Once);
            Assert.IsType(validator.GetType(), result);
        }

        [Fact]
        public void ProcessorSmokeTest()
        {
            var processor = new TestProcessor();

            ServiceProviderMock
                .Setup(sp => sp.GetService(processor.GetType()))
                .Returns(() => processor);

            var provider = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            var result = provider.GetValidator(processor.GetType().Name);

            ServiceProviderMock
                .Verify(sp => sp.GetService(processor.GetType()), Times.Once);
            Assert.IsType(processor.GetType(), result);
        }

        [Fact]
        public void ThrowsOnNullArgument()
        {
            var provider = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            Assert.Throws<ArgumentNullException>(() => provider.GetValidator(null));
        }

        [Fact]
        public void ThrowsOnUnknownValidator()
        {
            const string validatorName = "someNonExistentValidator";
            var provider = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            var ex = Assert.Throws<ArgumentException>(() => provider.GetValidator(validatorName));
            Assert.Contains(validatorName, ex.Message);
        }

        public ValidationProviderFacts()
        {
            ServiceProviderMock = new Mock<IServiceProvider>();
            LoggerMock = new Mock<ILogger<ValidatorProvider>>();

        }

        public class TestValidator : IValidator
        {
            public Task CleanUpAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<IValidationResult> GetResultAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<IValidationResult> StartAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }
        }

        public class TestProcessor : IProcessor
        {
            public Task CleanUpAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<IValidationResult> GetResultAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<IValidationResult> StartAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }
        }
    }
}
