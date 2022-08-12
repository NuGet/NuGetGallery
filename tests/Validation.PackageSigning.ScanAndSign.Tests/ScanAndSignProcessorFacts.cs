// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class TheCleanUpAsyncMethod : ScanAndSignProcessorFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.CleanUpAsync(null));
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task DeletesNothingWhenThereIsNoNupkgUrl()
        {
            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://nuget.test/package.nupkg");
            var status = new ValidatorStatus
            {
                State = ValidationStatus.Incomplete,
                NupkgUrl = null,
                ValidatorIssues = new List<ValidatorIssue>()
            };

            _validatorStateServiceMock
                .Setup(v => v.GetStatusAsync(request))
                .ReturnsAsync(status);

            await _target.CleanUpAsync(request);

            _validatorStateServiceMock.Verify(x => x.GetStatusAsync(request), Times.Once);
            _blobProvider.Verify(x => x.GetBlobFromUrl(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task WhenThereIsNupkgUrl_DeletesTheBlobIfRepositorySigningIsEnabled(bool repositorySigningEnabled, bool expectsBlobDeleted)
        {
            // Arrange
            _config.RepositorySigningEnabled = repositorySigningEnabled;

            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://nuget.test/package.nupkg");
            var nupkgUrl = "http://example/packages/nuget.versioning.4.6.0.nupkg";

            var status = new ValidatorStatus
            {
                State = ValidationStatus.Incomplete,
                NupkgUrl = nupkgUrl,
                ValidatorIssues = new List<ValidatorIssue>()
            };

            _validatorStateServiceMock
                .Setup(v => v.GetStatusAsync(request))
                .ReturnsAsync(status);

            var blob = new Mock<ISimpleCloudBlob>();

            _blobProvider
                .Setup(x => x.GetBlobFromUrl(nupkgUrl))
                .Returns(blob.Object);

            // Act
            await _target.CleanUpAsync(request);

            // Assert
            _validatorStateServiceMock.Verify(x => x.GetStatusAsync(request), Times.Once);

            if (expectsBlobDeleted)
            {
                _blobProvider.Verify(x => x.GetBlobFromUrl(nupkgUrl), Times.Once);
                blob.Verify(x => x.DeleteIfExistsAsync(), Times.Once);
            }
            else
            {
                _blobProvider.Verify(x => x.GetBlobFromUrl(nupkgUrl), Times.Never);
                blob.Verify(x => x.DeleteIfExistsAsync(), Times.Never);
            }
        }
    }

    public class TheGetResultAsyncMethod : ScanAndSignProcessorFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.GetResponseAsync(null));
            Assert.Equal("request", ex.ParamName);
        }

        [Fact]
        public async Task ForwardsCallToValidatorStateService()
        {
            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
            var status = new ValidatorStatus
            {
                State = ValidationStatus.Incomplete,
                NupkgUrl = null,
                ValidatorIssues = new List<ValidatorIssue>()
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<NuGetValidationRequest>()), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
            Assert.Empty(result.Issues);
            Assert.Equal(status.State, result.Status);
            Assert.Equal(status.NupkgUrl, result.NupkgUrl);
        }

        [Fact]
        public async Task DoesNotSkipCheckWhenPackageFitsCriteria()
        {
            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
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

            var result = await _target.GetResponseAsync(request);

            Assert.Equal(ValidationStatus.NotStarted, result.Status);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<NuGetValidationRequest>()), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task WhenRepositorySigningIsDisabled_SuppressesNupkgUrl()
        {
            _config.RepositorySigningEnabled = false;

            var request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
            var status = new ValidatorStatus
            {
                State = ValidationStatus.Incomplete,
                NupkgUrl = "https://nuget.test/package.nupkg",
                ValidatorIssues = new List<ValidatorIssue>()
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<NuGetValidationRequest>()), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
            Assert.Empty(result.Issues);
            Assert.Equal(status.State, result.Status);
            Assert.Null(result.NupkgUrl);
        }
    }

    public class TheStartAsyncMethod : ScanAndSignProcessorFactsBase
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
                .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<TimeSpan>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.AddStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryUpdateValidationStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);

            Assert.Equal(_status.State, result.Status);
            Assert.Equal(_status.NupkgUrl, result.NupkgUrl);
        }

        [Fact]
        public async Task EnqueuesScanAndSignIfPackageHasNoRepositorySignature()
        {
            _config.RepositorySigningEnabled = true;

            _validationContext.Mock();
            _packageServiceMock
                .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                .Returns(_packageRegistration);

            var result = await _target.StartAsync(_request);

            _packageServiceMock
                .Verify(p => p.FindPackageRegistrationById(_request.PackageId), Times.Once);

            _enqueuerMock
                .Verify(e =>
                    e.EnqueueScanAndSignAsync(
                        _request.ValidationId,
                        _request.NupkgUrl,
                        _config.V3ServiceIndexUrl,
                        It.Is<List<string>>(l =>
                            // Ensure that the owners are lexicographically ordered.
                            l.Count() == 4 &&
                            l[0] == "Annie" &&
                            l[1] == "Bob" &&
                            l[2] == "zack" &&
                            l[3] == "Zorro"),
                        It.Is<IReadOnlyDictionary<string, string>>(context => ValidateContext(context))),
                    Times.Once);

            _validatorStateServiceMock
                .Verify(v => v.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
            _validatorStateServiceMock
                .Verify(v => v.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
        }

        [Fact]
        public async Task EnqueuesScanAndSignEvenIfRepositorySigningIsDisabled()
        {
            _config.RepositorySigningEnabled = false;

            _validationContext.Mock();
            _packageServiceMock
                .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                .Returns(_packageRegistration);

            var result = await _target.StartAsync(_request);

            _packageServiceMock
                .Verify(p => p.FindPackageRegistrationById(_request.PackageId), Times.Once);

            _enqueuerMock
                .Verify(e =>
                    e.EnqueueScanAndSignAsync(
                        _request.ValidationId,
                        _request.NupkgUrl,
                        _config.V3ServiceIndexUrl,
                        It.Is<List<string>>(l =>
                            // Ensure that the owners are lexicographically ordered.
                            l.Count() == 4 &&
                            l[0] == "Annie" &&
                            l[1] == "Bob" &&
                            l[2] == "zack" &&
                            l[3] == "Zorro"),
                        It.Is<IReadOnlyDictionary<string, string>>(context => ValidateContext(context))),
                    Times.Once);

            _validatorStateServiceMock
                .Verify(v => v.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
            _validatorStateServiceMock
                .Verify(v => v.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
        }

        [Fact]
        public async Task ThrowsIfPackageHasNoRepositorySignatureAndNoPackageRegistration()
        {
            _config.RepositorySigningEnabled = true;

            _validationContext.Mock();
            _packageServiceMock
                .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                .Returns<PackageRegistration>(null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.StartAsync(_request));

            _packageServiceMock
                .Verify(p => p.FindPackageRegistrationById(_request.PackageId), Times.Once);

            _enqueuerMock
                .Verify(e =>
                    e.EnqueueScanAndSignAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<List<string>>(),
                        It.IsAny<IReadOnlyDictionary<string, string>>()),
                    Times.Never);

            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);
        }

        [Fact]
        public async Task EnqueuesScanIfPackageHasARepositorySignature()
        {
            _validationContext.Mock(packageSignatures: new[] {
                new PackageSignature
                {
                    PackageKey = _request.PackageKey,
                    Type = PackageSignatureType.Repository,
                }
            });

            _packageServiceMock
                .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                .Returns(_packageRegistration);

            var result = await _target.StartAsync(_request);

            _packageServiceMock
                .Verify(p => p.FindPackageRegistrationById(It.IsAny<string>()), Times.Once);

            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(
                        _request.ValidationId,
                        _request.NupkgUrl,
                        It.Is<IReadOnlyDictionary<string, string>>(context => ValidateContext(context))),
                    Times.Once);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyDictionary<string, string>>(),
                        It.IsAny<TimeSpan>()),
                    Times.Never);

            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
        }

        [Fact]
        public async Task WhenPackageIsRepositorySigned_SkipsCheckWhenPackageFitsCriteria()
        {
            _validationContext.Mock(packageSignatures: new[] {
                new PackageSignature
                {
                    PackageKey = _request.PackageKey,
                    Type = PackageSignatureType.Repository,
                }
            });

            _packageServiceMock
                .Setup(p => p.FindPackageRegistrationById(_request.PackageId))
                .Returns(_packageRegistration);

            _criteriaEvaluatorMock
                .Setup(ce => ce.IsMatch(It.IsAny<ICriteria>(), It.IsAny<Package>()))
                .Returns(false);

            var result = await _target.StartAsync(_request);

            Assert.Equal(ValidationStatus.Succeeded, result.Status);

            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<TimeSpan>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]

        public async Task WhenPackageFitsCriteriaAndIsNotRepositorySigned_DoesNotSkipScanAndSign(bool repositorySigningEnabled)
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
                .Verify(e => e.EnqueueScanAndSignAsync(
                        _request.ValidationId,
                        _request.NupkgUrl,
                        _config.V3ServiceIndexUrl,
                        It.IsAny<List<string>>(),
                        It.IsAny<IReadOnlyDictionary<string, string>>()),
                    Times.Once);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<IReadOnlyDictionary<string, string>>(),
                        It.IsAny<TimeSpan>()),
                    Times.Never);

            _validatorStateServiceMock
                .Verify(v =>
                    v.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), ValidationStatus.Incomplete),
                    Times.Once);
        }

        private NuGetValidationRequest _request;
        private ValidatorStatus _status;
        private PackageRegistration _packageRegistration = new PackageRegistration
        {
            Owners = new List<User>
            {
                new User("Zorro"),
                new User("Bob"),
                new User("Annie"),
                new User("zack")
            }
        };

        private bool ValidateContext(IReadOnlyDictionary<string, string> context) =>
            context.ContainsKey("ProductName") && context["ProductName"] == _request.PackageId
            && context.ContainsKey("ProductVersion") && context["ProductVersion"] == _request.PackageVersion
            && context.ContainsKey("ProductOwners") && context["ProductOwners"] == "Annie,Bob,zack,Zorro";

        public TheStartAsyncMethod()
        {
            _request = new NuGetValidationRequest(Guid.NewGuid(), 42, "somepackage", "somversion", "https://example.com/package.nupkg");
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
                .Setup(vss => vss.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()))
                .ReturnsAsync(_status);
        }
    }

    public class ScanAndSignProcessorFactsBase
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
        protected readonly ScanAndSignProcessor _target;

        public ScanAndSignProcessorFactsBase()
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

            _target = new ScanAndSignProcessor(
                _validationContext.Object,
                _validatorStateServiceMock.Object,
                _packageServiceMock.Object,
                _criteriaEvaluatorMock.Object,
                _enqueuerMock.Object,
                _blobProvider.Object,
                _optionsMock.Object,
                _loggerMock.Object);
        }
    }
}
