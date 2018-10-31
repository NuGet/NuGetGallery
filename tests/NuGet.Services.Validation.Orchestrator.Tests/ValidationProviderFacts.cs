// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationProviderFacts
    {
        public class Constructor
        {
            private readonly ITestOutputHelper _output;

            public Constructor(ITestOutputHelper output)
            {
                _output = output ?? throw new ArgumentNullException(nameof(output));
            }

            [Fact]
            public void CachesEvaluatedTypes()
            {
                var messages = new ConcurrentBag<string>();
                var serviceProvider = new Mock<IServiceProvider>();
                var loggerFactory = new LoggerFactory().AddXunit(_output);
                var innerLogger = loggerFactory.CreateLogger<ValidatorProvider>();
                var logger = new RecordingLogger<ValidatorProvider>(innerLogger);

                _output.WriteLine("Initializing the first instance.");

                var targetA = new ValidatorProvider(serviceProvider.Object, logger);
                var messageCountA = logger.Messages.Count;

                _output.WriteLine("Initializing the second instance.");

                var targetB = new ValidatorProvider(serviceProvider.Object, logger);
                var messageCountB = logger.Messages.Count;

                Assert.Equal(messageCountA, messageCountB);
            }
        }

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
            /// <summary>
            /// Names of known processors. These must never change unless there is a well thought out migration story
            /// or data fix. These names are encoded into DB tables used for orchestrator bookkeeping.
            /// </summary>
            [Theory]
            [InlineData("PackageSigningValidator", true)]
            [InlineData("PackageCertificatesValidator", false)]
            public void KnownValidatorsDoNotChangeNames(string validatorName, bool isProcessor)
            {
                Assert.True(Target.IsValidator(validatorName));
                Assert.Equal(isProcessor, Target.IsProcessor(validatorName));
            }

            [Fact]
            public void CanGetValidator()
            {
                var validator = new TestValidator();
                var validatorType = validator.GetType();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(validatorType))
                    .Returns(() => validator);

                var result = Target.GetValidator(ValidatorUtility.GetValidatorName(validatorType));

                ServiceProviderMock
                    .Verify(sp => sp.GetService(validatorType), Times.Once);
                Assert.IsType(validatorType, result);
            }

            [Fact]
            public void CanGetProcessor()
            {
                var processor = new TestProcessor();
                var processorType = processor.GetType();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(processorType))
                    .Returns(() => processor);
                
                var result = Target.GetValidator(ValidatorUtility.GetValidatorName(processorType));

                ServiceProviderMock
                    .Verify(sp => sp.GetService(processorType), Times.Once);
                Assert.IsType(processorType, result);
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

        [ValidatorName("TestValidator")]
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

        [ValidatorName("TestProcessor")]
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
