// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.ContentScan;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.ContentScan;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGetGallery;
using Tests.ContextHelpers;
using Xunit;

namespace Validation.ContentScan.Tests
{
    public class TheCleanUpAsyncMethod : ContentScanValidatorFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.CleanUpAsync(null));
            Assert.Equal("request", ex.ParamName);
        }
    }

    public class TheGetResultAsyncMethod : ContentScanValidatorFactsBase
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
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/package.nupkg"));
            var status = new ValidatorStatus
            {
                State = ValidationStatus.Incomplete,
                NupkgUrl = null
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<Guid>()), Times.Never);
            Assert.Equal(status.State, result.Status);
        }

        [Theory]
        [InlineData(ValidationStatus.Incomplete)]
        [InlineData(ValidationStatus.Succeeded)]
        [InlineData(ValidationStatus.Failed)]
        public async Task ReturnCorrectStatus(ValidationStatus validationStatus)
        {
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/package.nupkg"));
            var status = new ValidatorStatus
            {
                State = validationStatus,
                NupkgUrl = null
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);
           
            Assert.Equal(status.State, result.Status);
        }

        [Theory]
        [InlineData(ValidationStatus.Incomplete)]
        [InlineData(ValidationStatus.NotStarted)]
        [InlineData(ValidationStatus.Succeeded)]
        public async Task DoesNotReturnPackageValidationResultForNonTerminalAndSuccessState(ValidationStatus validationStatus)
        {
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/package.nupkg"));
            var status = new ValidatorStatus
            {
                State = validationStatus,
                NupkgUrl = null
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);

            Assert.Equal(status.State, result.Status);
            Assert.Empty(result.Results);
        }

        [Theory]
        [InlineData(ValidationStatus.Failed)]
        public async Task DoesReturnPackageValidationResultForFailedState(ValidationStatus validationStatus)
        {
            var request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/package.nupkg"));
            var status = new ValidatorStatus
            {
                State = validationStatus,
                NupkgUrl = null
            };

            _validatorStateServiceMock
                .Setup(vss => vss.GetStatusAsync(request))
                .ReturnsAsync(status);

            var result = await _target.GetResponseAsync(request);

            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(request), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.GetStatusAsync(It.IsAny<ValidationRequest>()), Times.Once);

            Assert.Equal(status.State, result.Status);
            Assert.NotEmpty(result.Results);
        }

    }

    public class TheStartAsyncMethod : ContentScanValidatorFactsBase
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
        public async Task DoesNotEnqueueNewOperationIfOneAlreadyExists(ValidationStatus status, string inputUrl)
        {
            _status.State = status;
            _status.NupkgUrl = inputUrl;

            var result = await _target.StartAsync(_request);

            _enqueuerMock
                .Verify(e => e.EnqueueContentScanAsync(It.IsAny<Guid>(), It.IsAny<Uri>()), Times.Never);
            _enqueuerMock
                .Verify(e => e.EnqueueContentScanAsync(It.IsAny<Guid>(), It.IsAny<Uri>(), It.IsAny<TimeSpan>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.AddStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.SaveStatusAsync(It.IsAny<ValidatorStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);
            _validatorStateServiceMock
                .Verify(vss => vss.TryUpdateValidationStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Never);

            Assert.Equal(_status.State, result.Status);
        }

        [Fact]
        public async Task EnqueueNewOperationIfNotYetStarted()
        {
            _status.State = ValidationStatus.NotStarted;
            _status.NupkgUrl = "https://example.com/package.nupkg";

            var result = await _target.StartAsync(_request);

            _enqueuerMock
                .Verify(e => e.EnqueueContentScanAsync(It.IsAny<Guid>(), It.IsAny<Uri>()), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);

            Assert.Equal(_status.State, result.Status);
        }

        private ValidationRequest _request;
        private ValidatorStatus _status;

        public TheStartAsyncMethod()
        {
            _request = new ValidationRequest(Guid.NewGuid(), new Uri("https://example.com/package.nupkg"));
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

    public class ContentScanValidatorFactsBase
    {
        protected readonly Mock<IValidationEntitiesContext> _validationContext;
        protected readonly Mock<IValidatorStateService> _validatorStateServiceMock;
        protected readonly Mock<IContentScanEnqueuer> _enqueuerMock;
        protected readonly Mock<IOptionsSnapshot<ContentScanConfiguration>> _optionsMock;
        protected readonly Mock<ILogger<ContentScanValidator>> _loggerMock;
        protected readonly ContentScanConfiguration _config;
        protected readonly ContentScanValidator _target;

        public ContentScanValidatorFactsBase()
        {
            _validationContext = new Mock<IValidationEntitiesContext>();
            _validatorStateServiceMock = new Mock<IValidatorStateService>();
            _enqueuerMock = new Mock<IContentScanEnqueuer>();
            _loggerMock = new Mock<ILogger<ContentScanValidator>>();
            _optionsMock = new Mock<IOptionsSnapshot<ContentScanConfiguration>>();
            _loggerMock = new Mock<ILogger<ContentScanValidator>>();

            _config = new ContentScanConfiguration();

            _optionsMock.Setup(o => o.Value).Returns(_config);

            _target = new ContentScanValidator(
                _validationContext.Object,
                _validatorStateServiceMock.Object,
                _enqueuerMock.Object,
                _optionsMock.Object,
                _loggerMock.Object);
        }
    }
}
