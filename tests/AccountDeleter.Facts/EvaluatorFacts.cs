// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.AccountDeleter.Facts
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

        [Fact]
        public void NuGetDeleteevaluatorReturnsCorrectValueForUnconfirmed()
        {
            // Setup
            _packageService.Setup(ps => ps.FindPackagesByOwner(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    return new List<Package>();
                });

            var packageService = _packageService.Object;
            var accountDeleteTelemetryService = _accountDeleteTelemetryService.Object;
            var logger = _loggerFactory.CreateLogger<NuGetDeleteEvaluator>();
            var testUser = new User();
            testUser.Organizations = new List<Membership>();

            var evaluator = new NuGetDeleteEvaluator(packageService, logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void NuGetDeleteevaluatorReturnsCorrectValueForPackages(bool userHasPackages, bool expectedResult)
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
            var logger = _loggerFactory.CreateLogger<NuGetDeleteEvaluator>();
            var testUser = new User()
            {
                EmailAddress = "test@test.domain"
            };
            testUser.Organizations = new List<Membership>();

            var evaluator = new NuGetDeleteEvaluator(packageService, logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(true, false, true)]
        public void NuGetDeleteevaluatorReturnsCorrectValueForOrganizations(bool userHasOrgs, bool userIsAdmin, bool expectedResult)
        {
            // Setup
            _packageService.Setup(ps => ps.FindPackagesByOwner(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    return new List<Package>();
                });

            var packageService = _packageService.Object;
            var accountDeleteTelemetryService = _accountDeleteTelemetryService.Object;
            var logger = _loggerFactory.CreateLogger<NuGetDeleteEvaluator>();
            var testUser = new User()
            {
                EmailAddress = "test@test.domain"
            };

            if (userHasOrgs)
            {
                testUser.Organizations = new List<Membership>()
                {
                    new Membership()
                    {
                        IsAdmin = userIsAdmin
                    }
                };
            }
            else
            {
                testUser.Organizations = new List<Membership>();
            }

            var evaluator = new NuGetDeleteEvaluator(packageService, logger);

            // Act
            var result = evaluator.CanUserBeDeleted(testUser);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void AlwayRejectevaluatorReturnsFalse()
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
        public void AlwayAllowevaluatorReturnsTrue()
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
    }
}
