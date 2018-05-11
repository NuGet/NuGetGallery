// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Orchestrator;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using Xunit;

namespace Validation.PackageSigning.ScanAndSign.Tests
{
    public class TheScanAndSignProcessorConstructor : ScanAndSignProcessorFactsBase
    {
        [Fact]
        public void ThrowsWhenValidatorStateServiceIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignProcessor(
                null,
                _enqueuerMock.Object,
                _loggerMock.Object));

            Assert.Equal("validatorStateService", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenScanAndSignEnqueuerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignProcessor(
                _validatorStateServiceMock.Object,
                null,
                _loggerMock.Object));

            Assert.Equal("scanAndSignEnqueuer", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenLoggerIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ScanAndSignProcessor(
                _validatorStateServiceMock.Object,
                _enqueuerMock.Object,
                null));

            Assert.Equal("logger", ex.ParamName);
        }
    }

    public class TheCleanUpAsyncMethod : ScanAndSignProcessorFactsBase
    {
        [Fact]
        public async Task ThrowsWhenRequestIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => _target.CleanUpAsync(null));
            Assert.Equal("request", ex.ParamName);
        }
    }

    public class TheGetResultAsyncMethod : ScanAndSignProcessorFactsBase
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
                .Verify(e => e.EnqueueScanAsync(It.IsAny<IValidationRequest>()), Times.Never);
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
        public async Task EnqueuesNewOperations()
        {
            var result = await _target.StartAsync(_request);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(_request), Times.Once);
            _enqueuerMock
                .Verify(e => e.EnqueueScanAsync(It.IsAny<IValidationRequest>()), Times.Once);

            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(_request, _status, ValidationStatus.Incomplete), Times.Once);
            _validatorStateServiceMock
                .Verify(vss => vss.TryAddValidatorStatusAsync(It.IsAny<IValidationRequest>(), It.IsAny<ValidatorStatus>(), It.IsAny<ValidationStatus>()), Times.Once);
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

    public class ScanAndSignProcessorFactsBase
    {
        protected Mock<IValidatorStateService> _validatorStateServiceMock;
        protected Mock<IScanAndSignEnqueuer> _enqueuerMock;
        protected Mock<ILogger<ScanAndSignProcessor>> _loggerMock;
        protected ScanAndSignProcessor _target;

        public ScanAndSignProcessorFactsBase()
        {
            _validatorStateServiceMock = new Mock<IValidatorStateService>();
            _enqueuerMock = new Mock<IScanAndSignEnqueuer>();
            _loggerMock = new Mock<ILogger<ScanAndSignProcessor>>();

            _target = new ScanAndSignProcessor(
                _validatorStateServiceMock.Object,
                _enqueuerMock.Object,
                _loggerMock.Object);
        }
    }
}
