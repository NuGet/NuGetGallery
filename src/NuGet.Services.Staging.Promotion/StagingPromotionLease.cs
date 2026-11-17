// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation.Leases;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Holds a promotion lease for the lifetime of one message, including long-running storage operations.
    /// </summary>
    public class StagingPromotionLease : IStagingPromotionLease
    {
        private static readonly TimeSpan LeaseTime = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(20);

        private readonly ILeaseService _leases;
        private readonly ILogger<StagingPromotionLease> _logger;
        private string _resourceName;
        private string _leaseId;
        private Exception _renewalFailure;

        /// <summary>
        /// Initializes a renewable promotion lease using the shared blob lease service.
        /// </summary>
        public StagingPromotionLease(ILeaseService leases, ILogger<StagingPromotionLease> logger)
        {
            _leases = leases ?? throw new ArgumentNullException(nameof(leases));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> RunAsync(string resourceName, Func<Task<bool>> process)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentException("A promotion lease resource name is required.", nameof(resourceName));
            }

            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            if (_resourceName != null)
            {
                throw new InvalidOperationException("This message already holds a promotion lease.");
            }

            var lease = await _leases.TryAcquireAsync(resourceName, LeaseTime, CancellationToken.None);
            if (!lease.IsSuccess)
            {
                _logger.LogInformation("Promotion lease {ResourceName} is held by another worker; retrying the message.", resourceName);
                return false;
            }

            _resourceName = resourceName;
            _leaseId = lease.LeaseId;
            _renewalFailure = null;
            using (var stopRenewal = new CancellationTokenSource())
            {
                var renewal = RenewUntilStoppedAsync(stopRenewal.Token);
                try
                {
                    await EnsureHeldAsync();
                    var handled = await process();
                    await EnsureHeldAsync();
                    return handled;
                }
                finally
                {
                    stopRenewal.Cancel();
                    await renewal;
                    try
                    {
                        if (!await _leases.ReleaseAsync(resourceName, _leaseId, CancellationToken.None))
                        {
                            _logger.LogWarning("Promotion lease {ResourceName} could not be released by this worker.", resourceName);
                        }
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to release promotion lease {ResourceName}; it will expire.", resourceName);
                    }
                    finally
                    {
                        _resourceName = null;
                        _leaseId = null;
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task EnsureHeldAsync()
        {
            if (_resourceName == null)
            {
                throw new InvalidOperationException("The message does not hold a promotion lease.");
            }

            var failure = Volatile.Read(ref _renewalFailure);
            if (failure != null)
            {
                throw new InvalidOperationException("Promotion lease renewal failed; publication must be retried.", failure);
            }

            var renewed = await _leases.RenewAsync(_resourceName, _leaseId, CancellationToken.None);
            if (!renewed.IsSuccess)
            {
                throw new InvalidOperationException("The promotion lease was lost; publication must be retried.");
            }
        }

        private async Task RenewUntilStoppedAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(RenewalInterval, cancellationToken);
                    await EnsureHeldAsync();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _renewalFailure, exception);
                _logger.LogError(exception, "Lost promotion lease {ResourceName}; publication must be retried.", _resourceName);
            }
        }
    }
}
