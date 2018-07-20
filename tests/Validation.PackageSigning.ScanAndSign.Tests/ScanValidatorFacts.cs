// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Vcs;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class ScanValidatorFacts
    {
        public class TheGetResultAsyncMethod : ScanValidatorFactsBase
        {
            [Fact]
            public async Task ThrowsWhenRequestIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.GetResultAsync(null));
                Assert.Equal("request", ex.ParamName);
            }

            [Fact]
            public async Task ForwardsCallToValidatorStateService()
            {
                var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
                var status = new ValidatorStatus
                {
                    State = ValidationStatus.Incomplete,
                    NupkgUrl = null,
                    ValidatorIssues = new List<ValidatorIssue>()
                };

                _validatorStateServiceMock
                    .Setup(vss => vss.GetStatusAsync(request))
                    .ReturnsAsync(status);

                var result = await _target.GetResultAsync(request);

                _validatorStateServiceMock
                    .Verify(vss => vss.GetStatusAsync(request), Times.Once);
                _validatorStateServiceMock
                    .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);
                _validatorStateServiceMock
                    .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
                Assert.Empty(result.Issues);
                Assert.Equal(status.State, result.Status);
                Assert.Equal(status.NupkgUrl, result.NupkgUrl);
            }

            [Fact]
            public async Task DoesNotSkipCheckWhenPackageFitsCriteria()
            {
                var request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
                var status = new ValidatorStatus
                {
                    State = ValidationStatus.NotStarted,
                    NupkgUrl = null,
                    ValidatorIssues = new List<ValidatorIssue>()
                };

                _criteriaEvaluatorMock
                    .Setup(ce => ce.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(false);

                _validatorStateServiceMock
                    .Setup(vss => vss.GetStatusAsync(request))
                    .ReturnsAsync(status);

                var result = await _target.GetResultAsync(request);

                Assert.Equal(ValidationStatus.NotStarted, result.Status);

                _validatorStateServiceMock
                    .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);
                _validatorStateServiceMock
                    .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
            }
        }

        public class TheStartAsyncMethod : ScanValidatorFactsBase
        {
            [Fact]
            public async Task ThrowsWhenRequestIsNull()
            {
                var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.StartAsync(null));
                Assert.Equal("request", ex.ParamName);
            }

            [Theory]
            [InlineData(ValidationStatus.Incomplete, null)]
            [InlineData(ValidationStatus.Succeeded, "https://example.com/package-output.nupkg")]
            [InlineData(ValidationStatus.Failed, null)]
            public async Task DoesNotEnqueueNewOperationIfOneAlreadyExists(ValidationStatus status, string nupkgUrl)
            {
                _status.State = status;
                _status.NupkgUrl = nupkgUrl;

                var result = await _target.StartAsync(_request);

                _enqueuerMock
                    .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
                _validatorStateServiceMock
                    .Verify(vss => vss.AddStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
                _validatorStateServiceMock
                    .Verify(vss => vss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
                _validatorStateServiceMock
                    .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);
                _validatorStateServiceMock
                    .Verify(vss => vss.TryUpdateValidationStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);

                Assert.Equal(_status.State, result.Status);
                Assert.Equal(_status.NupkgUrl, result.NupkgUrl);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WhenPackageHasNoRepositorySignature_Scans(bool repositorySigningEnabled)
            {
                _config.RepositorySigningEnabled = repositorySigningEnabled;

                _validationContext.Mock();

                var result = await _target.StartAsync(_request);

                _enqueuerMock
                    .Verify(e => e.EnqueueScanAsync(_request.ValidationId, _request.NupkgUrl), Times.Once);

                _validatorStateServiceMock
                    .Verify(vss => vss.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
                _validatorStateServiceMock
                    .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
            }

            [Fact]
            public async Task WhenPackageHasARepositorySignature_Scans()
            {
                _validationContext.Mock(packageSignatures: new[]
                {
                    new PackageSignature
                    {
                        PackageKey = _request.PackageKey,
                        Type = PackageSignatureType.Repository,
                    }
                });

                var result = await _target.StartAsync(_request);

                _packageServiceMock
                    .Verify(p => p.FindPackageRegistrationById(It.IsAny<string>()), Times.Never);

                _enqueuerMock
                    .Verify(e => e.EnqueueScanAsync(_request.ValidationId, _request.NupkgUrl), Times.Once);

                _validatorStateServiceMock
                    .Verify(vss => vss.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
                _validatorStateServiceMock
                    .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]

            public async Task WhenPackageFitsCriteriaAndIsNotRepositorySigned_Skips(bool repositorySigningEnabled)
            {
                _config.RepositorySigningEnabled = repositorySigningEnabled;

                _validationContext.Mock();
                _packageServiceMock
                    .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                    .Returns(_packageRegistration);

                _criteriaEvaluatorMock
                    .Setup(ce => ce.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(false);

                await _target.StartAsync(_request);

                _enqueuerMock
                    .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);

                _enqueuerMock
                    .Verify(e => e.EnqueueScanAndSignAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);

                _validatorStateServiceMock
                    .Verify(v =>
                        v.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()),
                        Times.Never);
            }

            private ValidationRequest _request;
            private ValidatorStatus _status;
            private PackageRegistration _packageRegistration = new PackageRegistration();

            public TheStartAsyncMethod()
            {
                _request = new ValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
                _status = new ValidatorStatus
                {
                    State = ValidationStatus.NotStarted,
                    NupkgUrl = null,
                    ValidatorIssues = new List<ValidatorIssue>()
                };

                _validatorStateServiceMock
                    .Setup(vss => vss.GetStatusAsync(_request))
                    .ReturnsAsync(_status);
                _validatorStateServiceMock
                    .Setup(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()))
                    .ReturnsAsync(_status);
            }
        }

        public class ScanValidatorFactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<IValidatorStateService> _validatorStateServiceMock;
            protected readonly Mock<ICorePackageService> _packageServiceMock;
            protected Mock<ICriteriaEvaluator<Package>> _criteriaEvaluatorMock;
            protected readonly Mock<IScanAndSignEnqueuer> _enqueuerMock;
            protected readonly Mock<ISimpleCloudBlobProvider> _blobProvider;
            protected readonly Mock<IOptionsSnapshot<ScanAndSignConfiguration>> _optionsMock;
            protected readonly Mock<ILogger<ScanAndSignProcessor>> _loggerMock;
            protected readonly ScanAndSignConfiguration _config;
            protected readonly ScanValidator _target;

            public ScanValidatorFactsBase()
            {
                _validationContext = new Mock<IValidationEntitiesContext>();
                _validatorStateServiceMock = new Mock<IValidatorStateService>();
                _packageServiceMock = new Mock<ICorePackageService>();
                _criteriaEvaluatorMock = new Mock<ICriteriaEvaluator<Package>>();
                _enqueuerMock = new Mock<IScanAndSignEnqueuer>();
                _loggerMock = new Mock<ILogger<ScanAndSignProcessor>>();
                _blobProvider = new Mock<ISimpleCloudBlobProvider>();
                _optionsMock = new Mock<IOptionsSnapshot<ScanAndSignConfiguration>>();
                _loggerMock = new Mock<ILogger<ScanAndSignProcessor>>();

                _criteriaEvaluatorMock
                    .Setup(ce => ce.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                    .Returns(true);

                _config = new ScanAndSignConfiguration();

                _config.V3ServiceIndexUrl = "http://awesome.v3/service/index.json";

                _optionsMock.Setup(o => o.Value).Returns(_config);

                _target = new ScanValidator(
                    _validationContext.Object,
                    _validatorStateServiceMock.Object,
                    _packageServiceMock.Object,
                    _criteriaEvaluatorMock.Object,
                    _enqueuerMock.Object,
                    _optionsMock.Object,
                    _loggerMock.Object);
            }
        }
    }
}
