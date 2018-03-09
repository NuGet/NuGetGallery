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
        public class IsProcessor : BaseFacts
        {
            [Theory]
            [InlineData(nameof(TestProcessor), true)]
            [InlineData(nameof(TestValidator), false)]
            [InlineData(nameof(IsProcessor), false)]
            [InlineData(nameof(IProcessor), false)]
            [InlineData(nameof(IValidator), false)]
            [InlineData("NotARealType", false)]
            public void ReturnsTrueForProcessors(string name, bool expected)
            {
                Assert.Equal(expected, Target.IsProcessor(name));
            }
        }

        public class IsValidator : BaseFacts
        {
            [Theory]
            [InlineData(nameof(TestProcessor), true)]
            [InlineData(nameof(TestValidator), true)]
            [InlineData(nameof(IsProcessor), false)]
            [InlineData(nameof(IProcessor), false)]
            [InlineData(nameof(IValidator), false)]
            [InlineData("NotARealType", false)]
            public void ReturnsTrueForValidators(string name, bool expected)
            {
                Assert.Equal(expected, Target.IsValidator(name));
            }
        }

        public class GetValidator : BaseFacts
        {
            [Fact]
            public void CanGetValidator()
            {
                var validator = new TestValidator();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(validator.GetType()))
                    .Returns(() => validator);

                var result = Target.GetValidator(validator.GetType().Name);

                ServiceProviderMock
                    .Verify(sp => sp.GetService(validator.GetType()), Times.Once);
                Assert.IsType(validator.GetType(), result);
            }

            [Fact]
            public void CanGetProcessor()
            {
                var processor = new TestProcessor();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(processor.GetType()))
                    .Returns(() => processor);
                
                var result = Target.GetValidator(processor.GetType().Name);

                ServiceProviderMock
                    .Verify(sp => sp.GetService(processor.GetType()), Times.Once);
                Assert.IsType(processor.GetType(), result);
            }

            [Fact]
            public void ThrowsOnNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => Target.GetValidator(null));
            }

            [Fact]
            public void ThrowsOnUnknownValidator()
            {
                const string validatorName = "someNonExistentValidator";

                var ex = Assert.Throws<ArgumentException>(() => Target.GetValidator(validatorName));
                Assert.Contains(validatorName, ex.Message);
            }
        }

        public abstract class BaseFacts
        {
            public BaseFacts()
            {
                ServiceProviderMock = new Mock<IServiceProvider>();
                LoggerMock = new Mock<ILogger<ValidatorProvider>>();

                Target = new ValidatorProvider(ServiceProviderMock.Object, LoggerMock.Object);
            }

            protected Mock<IServiceProvider> ServiceProviderMock { get; }
            protected Mock<ILogger<ValidatorProvider>> LoggerMock { get; }
            public ValidatorProvider Target { get; }
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
