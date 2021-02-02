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

        public class IsNuGetProcessor : BaseFacts
        {
            [Theory]
            [InlineData(nameof(TestNuGetProcessor), true)]
            [InlineData(nameof(TestNuGetValidator), false)]
            [InlineData(nameof(IsNuGetProcessor), false)]
            [InlineData(nameof(INuGetProcessor), false)]
            [InlineData(nameof(INuGetValidator), false)]
            [InlineData("NotARealType", false)]
            public void ReturnsTrueForNuGetProcessors(string name, bool expected)
            {
                Assert.Equal(expected, Target.IsNuGetProcessor(name));
            }
        }

        public class IsNuGetValidator : BaseFacts
        {
            [Theory]
            [InlineData(nameof(TestNuGetProcessor), true)]
            [InlineData(nameof(TestNuGetValidator), true)]
            [InlineData(nameof(IsNuGetProcessor), false)]
            [InlineData(nameof(INuGetProcessor), false)]
            [InlineData(nameof(INuGetValidator), false)]
            [InlineData("NotARealType", false)]
            public void ReturnsTrueForNuGetValidators(string name, bool expected)
            {
                Assert.Equal(expected, Target.IsNuGetValidator(name));
            }
        }

        public class GetNuGetValidator : BaseFacts
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
                Assert.True(Target.IsNuGetValidator(validatorName));
                Assert.Equal(isProcessor, Target.IsNuGetProcessor(validatorName));
            }

            [Fact]
            public void CanGetValidator()
            {
                var validator = new TestNuGetValidator();
                var validatorType = validator.GetType();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(validatorType))
                    .Returns(() => validator);

                var result = Target.GetNuGetValidator(ValidatorUtility.GetValidatorName(validatorType));

                ServiceProviderMock
                    .Verify(sp => sp.GetService(validatorType), Times.Once);
                Assert.IsType(validatorType, result);
            }

            [Fact]
            public void CanGetProcessor()
            {
                var processor = new TestNuGetProcessor();
                var processorType = processor.GetType();

                ServiceProviderMock
                    .Setup(sp => sp.GetService(processorType))
                    .Returns(() => processor);
                
                var result = Target.GetNuGetValidator(ValidatorUtility.GetValidatorName(processorType));

                ServiceProviderMock
                    .Verify(sp => sp.GetService(processorType), Times.Once);
                Assert.IsType(processorType, result);
            }

            [Fact]
            public void ThrowsOnNullArgument()
            {
                Assert.Throws<ArgumentNullException>(() => Target.GetNuGetValidator(null));
            }

            [Fact]
            public void ThrowsOnUnknownValidator()
            {
                const string validatorName = "someNonExistentValidator";

                var ex = Assert.Throws<ArgumentException>(() => Target.GetNuGetValidator(validatorName));
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

        [ValidatorName("TestNuGetValidator")]
        public class TestNuGetValidator : INuGetValidator
        {
            public Task CleanUpAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }
        }

        [ValidatorName("TestNuGetProcessor")]
        public class TestNuGetProcessor : INuGetProcessor
        {
            public Task CleanUpAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<INuGetValidationResponse> GetResponseAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<INuGetValidationResponse> StartAsync(INuGetValidationRequest request)
            {
                throw new NotImplementedException();
            }
        }
    }
}
