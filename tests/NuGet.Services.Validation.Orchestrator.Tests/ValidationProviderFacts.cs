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
        public void SmokeTest()
        {
            ServiceProviderMock
                .Setup(sp => sp.GetService(typeof(TestValidator1)))
                .Returns(() => new TestValidator1());

            var provider = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            var result = provider.GetValidator(nameof(TestValidator1));
            ServiceProviderMock
                .Verify(sp => sp.GetService(typeof(TestValidator1)), Times.Once());

            Assert.IsType<TestValidator1>(result);
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

        public class TestValidator1 : IValidator
        {
            public Task<ValidationStatus> GetStatusAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<ValidationStatus> StartValidationAsync(IValidationRequest request)
            {
                throw new NotImplementedException();
            }
        }
    }
}
