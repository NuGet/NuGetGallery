// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.Common.Validators;
using NuGet.Jobs.Validation.Common.Validators.Vcs;
using NuGet.Services.VirusScanning.Core;
using Xunit;

namespace Validation.Common.Tests
{
    public class VcsValidatorFacts
    {
        public class TheNameProperty : FactsBase
        {
            [Fact]
            public void NeverChanges()
            {
                Assert.Equal("validator-vcs", _target.Name);
            }
        }

        public class TheValidateMethod : FactsBase
        {
            [Fact]
            public async Task PrefersTheDownloadUrlOverBuildingTheUrl()
            {
                // Arrange
                _message.Package.DownloadUrl = new Uri("http://example/some-custom-url");

                // Act
                var actual = await _target.ValidateAsync(_message, _auditEntries);

                // Assert
                Assert.Equal(ValidationResult.Asynchronous, actual);
                _scanningService.Verify(
                    x => x.CreateVirusScanJobAsync(
                        _message.Package.DownloadUrl.ToString(),
                        _callbackUrl,
                        $"NuGet - f470b9fb-0243-4f65-8aef-90d93dfe1a03 - NuGet.Versioning 3.4.0-ALPHA",
                        _message.ValidationId),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsAsynchronousOnSuccess()
            {
                // Arrange & Act
                var actual = await _target.ValidateAsync(_message, _auditEntries);

                // Assert
                Assert.Equal(ValidationResult.Asynchronous, actual);
                _scanningService.Verify(
                    x => x.CreateVirusScanJobAsync(
                        "http://example/packages/nuget.versioning/3.4.0-alpha/nuget.versioning.3.4.0-alpha.nupkg",
                        _callbackUrl,
                        $"NuGet - f470b9fb-0243-4f65-8aef-90d93dfe1a03 - NuGet.Versioning 3.4.0-ALPHA",
                        _message.ValidationId),
                    Times.Once);
                _scanningService.Verify(
                    x => x.CreateVirusScanJobAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Guid>()),
                    Times.Once);
            }

            [Fact]
            public async Task FailsWhenThereIsAnErrorMessage()
            {
                // Arrange
                var errorMessage = "Error!";
                _virusScanJob.ErrorMessage = errorMessage;

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ValidationException>(
                    () => _target.ValidateAsync(_message, _auditEntries));
                Assert.Equal(errorMessage, exception.Message);
            }

            [Fact]
            public async Task FailsIfAllResponseValuesAreNull()
            {
                // Arrange
                _virusScanJob.RequestId = null;
                _virusScanJob.JobId = null;
                _virusScanJob.RegionCode = null;

                // Act & Assert
                var exception = await Assert.ThrowsAsync<ValidationException>(
                    () => _target.ValidateAsync(_message, _auditEntries));
                Assert.Equal("The request had no request ID, job ID, and region code.", exception.Message);
            }

            [Fact]
            public async Task SucceedsIfOnlyRequestIdIsNull()
            {
                // Arrange
                _virusScanJob.RequestId = null;

                // Act
                var actual = await _target.ValidateAsync(_message, _auditEntries);

                // Assert
                Assert.Equal(ValidationResult.Asynchronous, actual);
            }

            [Fact]
            public async Task SucceedsIfOnlyJobIdIsNull()
            {
                // Arrange
                _virusScanJob.JobId = null;

                // Act
                var actual = await _target.ValidateAsync(_message, _auditEntries);

                // Assert
                Assert.Equal(ValidationResult.Asynchronous, actual);
            }

            [Fact]
            public async Task SucceedsIfOnlyRegionCodeIsNull()
            {
                // Arrange
                _virusScanJob.RegionCode = null;

                // Act
                var actual = await _target.ValidateAsync(_message, _auditEntries);

                // Assert
                Assert.Equal(ValidationResult.Asynchronous, actual);
            }
        }

        public abstract class FactsBase
        {
            protected readonly Uri _callbackUrl;
            protected readonly string _packageUrlTemplate;
            protected readonly Mock<IVirusScanningService> _scanningService;
            protected readonly Mock<ILogger<VcsValidator>> _logger;
            protected PackageValidationMessage _message;
            protected List<PackageValidationAuditEntry> _auditEntries;
            protected VirusScanJob _virusScanJob;
            protected readonly VcsValidator _target;

            public FactsBase()
            {
                _callbackUrl = new Uri("http://example/callback");
                _packageUrlTemplate = "http://example/packages/{id}/{version}/{id}.{version}.nupkg";
                _scanningService = new Mock<IVirusScanningService>();
                _logger = new Mock<ILogger<VcsValidator>>();

                _message = new PackageValidationMessage
                {
                    PackageId = "NuGet.Versioning",
                    PackageVersion = "3.4.0-ALPHA",
                    ValidationId = new Guid("f470b9fb-0243-4f65-8aef-90d93dfe1a03"),
                    Package = new NuGetPackage
                    {
                        Id = "NuGet.Versioning",
                        NormalizedVersion = "3.4.0-ALPHA"
                    }
                };
                _auditEntries = new List<PackageValidationAuditEntry>();
                _virusScanJob = new VirusScanJob
                {
                    JobId = "123",
                    RequestId = "456",
                    RegionCode = "USW",
                };

                _scanningService
                    .Setup(x => x.CreateVirusScanJobAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Guid>()))
                    .Returns(() => Task.FromResult(_virusScanJob));

                _target = new VcsValidator(
                    _callbackUrl,
                    _packageUrlTemplate,
                    _scanningService.Object,
                    _logger.Object);
            }
        }
    }
}
