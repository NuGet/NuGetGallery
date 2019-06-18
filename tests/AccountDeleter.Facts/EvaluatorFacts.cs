// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.AccountDeleter;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace AccountDeleter.Facts
{
    public class EvaluatorFacts
    {
        private readonly ITestOutputHelper _output;
        private Mock<IPackageService> _packageService;
        private Mock<IAccountDeleteTelemetryService> _accountDeleteTelemetryService;
        private ILoggerFactory _loggerFactory;

        public EvaluatorFacts(ITestOutputHelper output)
        {
            _output = output;
            _packageService = new Mock<IPackageService>();
            _accountDeleteTelemetryService = new Mock<IAccountDeleteTelemetryService>();

            _loggerFactory = new LoggerFactory()
                .AddXunit(_output);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void UserPackageEvaulatorReturnsCorrectValue(bool userHasPackages, bool expectedResult)
        {
            // Setup
            _packageService.Setup(ps => ps.FindPackagesByOwner(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    if (userHasPackages)
                    {
                        return new List<Package> { new Package() };
                    }

                    return new List<Package>();
                });

            var packageService = _packageService.Object;
            var accountDeleteTelemetryService = _accountDeleteTelemetryService.Object;
            var logger = _loggerFactory.CreateLogger<UserPackageEvaluator>();
            var testUser = new User();

            var evaluator = new UserPackageEvaluator(packageService, accountDeleteTelemetryService, logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void AlwayRejectEvaulatorReturnsFalse()
        {
            // Setup
            var logger = _loggerFactory.CreateLogger<AlwaysRejectEvaluator>();
            var testUser = new User();

            var evaluator = new AlwaysRejectEvaluator(logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AlwayAllowEvaulatorReturnsTrue()
        {
            // Setup
            var logger = _loggerFactory.CreateLogger<AlwaysAllowEvaluator>();
            var testUser = new User();

            var evaluator = new AlwaysAllowEvaluator(logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AggregateEvaluatorRejectsDuplicateEvalutors()
        {
            // Setup
            var aaeLogger = _loggerFactory.CreateLogger<AlwaysAllowEvaluator>();
            var areLogger = _loggerFactory.CreateLogger<AlwaysRejectEvaluator>();
            var aeLogger = _loggerFactory.CreateLogger<AggregateEvaluator>();

            var evaluator1 = new AlwaysAllowEvaluator(aaeLogger);
            var evaluator2 = new AlwaysRejectEvaluator(areLogger);

            var aggregateEvalutor = new AggregateEvaluator(aeLogger);

            // Act
            var result1 = aggregateEvalutor.AddEvaluator(evaluator1);
            var result2 = aggregateEvalutor.AddEvaluator(evaluator1);
            var result3 = aggregateEvalutor.AddEvaluator(evaluator2);

            // Assert
            Assert.True(result1);
            Assert.False(result2);
            Assert.True(result3);
        }

        [Fact]
        public void AggregateEvaluatorRejectsIfAnyRejects()
        {
            // Setup
            var aaeLogger = _loggerFactory.CreateLogger<AlwaysAllowEvaluator>();
            var areLogger = _loggerFactory.CreateLogger<AlwaysRejectEvaluator>();
            var aeLogger = _loggerFactory.CreateLogger<AggregateEvaluator>();
            var testUser = new User();

            var evaluator1 = new AlwaysAllowEvaluator(aaeLogger);
            var evaluator2 = new AlwaysRejectEvaluator(areLogger);

            var aggregateEvalutor = new AggregateEvaluator(aeLogger);
            aggregateEvalutor.AddEvaluator(evaluator1);
            aggregateEvalutor.AddEvaluator(evaluator2);

            // Act
            var result = aggregateEvalutor.CanUserBeDeleted(testUser);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AggregateEvaluatorAcceptsIfAllAccept()
        {
            // Setup
            var aaeLogger = _loggerFactory.CreateLogger<AlwaysAllowEvaluator>();
            var aae2Logger = _loggerFactory.CreateLogger<AlwaysAllowEvaluator>();
            var aeLogger = _loggerFactory.CreateLogger<AggregateEvaluator>();
            var testUser = new User();

            var evaluator1 = new AlwaysAllowEvaluator(aaeLogger);
            var evaluator2 = new AlwaysAllowEvaluator(aae2Logger);

            var aggregateEvalutor = new AggregateEvaluator(aeLogger);
            aggregateEvalutor.AddEvaluator(evaluator1);
            aggregateEvalutor.AddEvaluator(evaluator2);

            // Act
            var result = aggregateEvalutor.CanUserBeDeleted(testUser);

            // Assert
            Assert.True(result);
        }
    }
}
