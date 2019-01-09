// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring;
using Xunit;

namespace NgTests.Validation
{
    public class ValidatorTests
    {
        [Fact]
        public async Task Run_ReturnsPass()
        {
            // Arrange
            Func<bool> shouldRun = () => true;
            Action runInternal = () => { };

            var validator = new TestableValidator(shouldRun, runInternal);

            var context = CreateContext();

            // Act
            var result = await validator.ValidateAsync(context);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Pass, result.Result);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task Run_ReturnsSkip()
        {
            // Arrange
            Func<bool> shouldRun = () => false;
            Action runInternal = () => { };

            var validator = new TestableValidator(shouldRun, runInternal);

            var context = CreateContext();

            // Act
            var result = await validator.ValidateAsync(context);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Skip, result.Result);
            Assert.Null(result.Exception);
        }

        [Fact]
        public async Task Run_ReturnsFail()
        {
            // Arrange
            var exception = new Exception();

            Func<bool> shouldRun = () => true;
            Action runInternal = () => { throw exception; };

            var validator = new TestableValidator(shouldRun, runInternal);

            var context = CreateContext();

            // Act
            var result = await validator.ValidateAsync(context);

            // Assert
            Assert.Same(validator, result.Validator);
            Assert.Equal(TestResult.Fail, result.Result);
            Assert.Same(exception, result.Exception);
        }

        private static ValidationContext CreateContext()
        {
            return ValidationContextStub.Create();
        }
    }

    public class TestableValidator : Validator
    {
        private static readonly IDictionary<FeedType, SourceRepository> _feedToSource;
        private static readonly ILogger<Validator> _logger;
        private static readonly ValidatorConfiguration _validatorConfiguration;

        static TestableValidator()
        {
            var sourceRepository = new Mock<SourceRepository>();
            var metadataResource = new Mock<IPackageTimestampMetadataResource>();

            metadataResource.Setup(x => x.GetAsync(It.IsAny<ValidationContext>()))
                .ReturnsAsync(PackageTimestampMetadata.CreateForPackageExistingOnFeed(DateTime.Now, DateTime.Now));

            sourceRepository.Setup(x => x.GetResource<IPackageTimestampMetadataResource>())
                .Returns(metadataResource.Object);

            var feedToSource = new Mock<IDictionary<FeedType, SourceRepository>>();

            feedToSource.Setup(x => x[It.IsAny<FeedType>()]).Returns(sourceRepository.Object);

            _feedToSource = feedToSource.Object;
            _validatorConfiguration = new ValidatorConfiguration(packageBaseAddress: "a", requireRepositorySignature: true);
            _logger = Mock.Of<ILogger<Validator>>();
        }

        public TestableValidator(Func<bool> shouldRun, Action runInternal)
            : base(_validatorConfiguration, _logger)
        {
            _shouldRun = shouldRun;
            _runInternal = runInternal;
        }

        protected override Task<bool> ShouldRunAsync(ValidationContext context)
        {
            return Task.FromResult(_shouldRun());
        }

        protected override Task RunInternalAsync(ValidationContext context)
        {
            _runInternal();

            return Task.FromResult(0);
        }

        private Func<bool> _shouldRun;
        private Action _runInternal;
    }
}