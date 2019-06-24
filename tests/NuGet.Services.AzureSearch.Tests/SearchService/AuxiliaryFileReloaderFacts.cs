// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class AuxiliaryFileReloaderFacts
    {
        public class ReloadContinuouslyAsync : BaseFacts
        {
            public ReloadContinuouslyAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CanBeCancelledImmediately()
            {
                _cts.Cancel();

                await _target.ReloadContinuouslyAsync(_cts.Token);

                _cache.Verify(x => x.TryLoadAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            [Fact]
            public async Task ReloadsUntilCancelled()
            {
                CancelAfter(reloads: 5);
                _config.AuxiliaryDataReloadFrequency = TimeSpan.Zero;

                await _target.ReloadContinuouslyAsync(_cts.Token);

                _cache.Verify(x => x.TryLoadAsync(_cts.Token), Times.Exactly(5));
                _cache.Verify(x => x.TryLoadAsync(It.IsAny<CancellationToken>()), Times.Exactly(5));
            }

            [Fact]
            public async Task UsesReloadFrequencyOnSuccess()
            {
                CancelAfter(reloads: 2);
                _config.AuxiliaryDataReloadFrequency = TimeSpan.FromMilliseconds(100);
                _config.AuxiliaryDataReloadFailureRetryFrequency = TimeSpan.Zero;

                await _target.ReloadContinuouslyAsync(_cts.Token);

                _cache.Verify(x => x.TryLoadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
                _systemTime.Verify(x => x.Delay(_config.AuxiliaryDataReloadFrequency, _cts.Token), Times.Once);
                _systemTime.Verify(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task UsesReloadFailureRetryFrequencyOnSuccess()
            {
                int count = 0;
                _cache
                    .Setup(x => x.TryLoadAsync(It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        count++;
                        if (count >= 2)
                        {
                            _cts.Cancel();
                        }

                        throw new InvalidOperationException("Please retry later.");
                    });
                _config.AuxiliaryDataReloadFrequency = TimeSpan.Zero;
                _config.AuxiliaryDataReloadFailureRetryFrequency = TimeSpan.FromMilliseconds(100);

                await _target.ReloadContinuouslyAsync(_cts.Token);

                _cache.Verify(x => x.TryLoadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
                _systemTime.Verify(x => x.Delay(_config.AuxiliaryDataReloadFailureRetryFrequency, _cts.Token), Times.Once);
                _systemTime.Verify(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IAuxiliaryDataCache> _cache;
            protected readonly Mock<ISystemTime> _systemTime;
            protected readonly SearchServiceConfiguration _config;
            protected readonly Mock<IOptionsSnapshot<SearchServiceConfiguration>> _options;
            protected readonly RecordingLogger<AuxiliaryFileReloader> _logger;
            protected readonly CancellationTokenSource _cts;
            protected readonly AuxiliaryFileReloader _target;

            public BaseFacts(ITestOutputHelper output)
            {
                _cache = new Mock<IAuxiliaryDataCache>();
                _systemTime = new Mock<ISystemTime>();
                _config = new SearchServiceConfiguration();
                _options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                _logger = output.GetLogger<AuxiliaryFileReloader>();

                _cts = new CancellationTokenSource();

                // Default test behavior is to cancel after the first invocation otherwise it is very easy to loop
                // forever, which is annoying for the person writing the tests.
                CancelAfter(reloads: 1);

                _config.AuxiliaryDataReloadFrequency = TimeSpan.FromMilliseconds(100);
                _config.AuxiliaryDataReloadFailureRetryFrequency = TimeSpan.FromMilliseconds(20);
                _options.Setup(x => x.Value).Returns(() => _config);

                _target = new AuxiliaryFileReloader(
                    _cache.Object,
                    _systemTime.Object,
                    _options.Object,
                    _logger);
            }

            protected void CancelAfter(int reloads)
            {
                int count = 0;
                _cache
                    .Setup(x => x.TryLoadAsync(It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() =>
                    {
                        count++;
                        if (count >= reloads)
                        {
                            _cts.Cancel();
                        }
                    });
            }
        }
    }
}
