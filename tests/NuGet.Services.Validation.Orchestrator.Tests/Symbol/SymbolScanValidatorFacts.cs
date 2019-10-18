// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Symbols;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests.Symbol
{
    public class SymbolScanValidatorFacts
    {
        public class TheGetResultAsyncMethod : SymbolScanValidatorFactsBase
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
                    .Setup(ce => ce.IsMatch(It.IsAny<ICriteria>(), It.IsAny<SymbolPackage>()))
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
        public class TheStartAsyncMethod : SymbolScanValidatorFactsBase
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

                _scanAndSignEnqueuer
                    .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
                _scanAndSignEnqueuer
                    .Verify(e => e.EnqueueScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
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

            [Fact]
            public async Task ThrowsWhenValidatingSymbolPackageNotFound()
            {
                _galleryService
                    .Setup(x => x.FindSymbolPackagesByIdAndVersion(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(new List<SymbolPackage>());

                await Assert.ThrowsAsync<InvalidDataException>(async () => await _target.StartAsync(_request));
            }

            private ValidationRequest _request;
            private ValidatorStatus _status;

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

        public abstract class SymbolScanValidatorFactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationContext;
            protected readonly Mock<ICoreSymbolPackageService> _galleryService;
            protected readonly Mock<ICriteriaEvaluator<SymbolPackage>> _criteriaEvaluatorMock;
            protected readonly Mock<IScanAndSignEnqueuer> _scanAndSignEnqueuer;
            protected readonly Mock<IOptionsSnapshot<SymbolScanOnlyConfiguration>> _configurationAccessor;
            protected readonly Mock<IValidatorStateService> _validatorStateServiceMock;
            protected readonly Mock<ILogger<ScanAndSignProcessor>> _loggerMock;
            protected readonly SymbolScanOnlyConfiguration _config;
            protected readonly SymbolScanValidator _target;

            public SymbolScanValidatorFactsBase()
            {
                _validationContext = new Mock<IValidationEntitiesContext>();
                _galleryService = new Mock<ICoreSymbolPackageService>();
                _criteriaEvaluatorMock = new Mock<ICriteriaEvaluator<SymbolPackage>>();
                _scanAndSignEnqueuer = new Mock<IScanAndSignEnqueuer>();
                _configurationAccessor = new Mock<IOptionsSnapshot<SymbolScanOnlyConfiguration>>();
                _validatorStateServiceMock = new Mock<IValidatorStateService>();
                _loggerMock = new Mock<ILogger<ScanAndSignProcessor>>();

                _config = new SymbolScanOnlyConfiguration();
                _config.V3ServiceIndexUrl = "http://awesome.v3/service/index.json";

                _configurationAccessor.Setup(o => o.Value).Returns(_config);
                _configurationAccessor.Setup(c => c.Value).Returns(new SymbolScanOnlyConfiguration());

                _target = new SymbolScanValidator(
                    _validationContext.Object,
                    _validatorStateServiceMock.Object,
                    _galleryService.Object,
                    _criteriaEvaluatorMock.Object,
                    _scanAndSignEnqueuer.Object,
                    _configurationAccessor.Object,
                    _loggerMock.Object);
            }
        }
    }
}
