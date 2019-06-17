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
    }
}
