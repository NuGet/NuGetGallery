// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Leases;

namespace NuGet.Services.Staging.Promotion.Tests
{
    /// <summary>
    /// Models shared storage ownership for independent worker scopes.
    /// </summary>
    internal class TestLeaseService : ILeaseService
    {
        private readonly Dictionary<string, string> _leases = new Dictionary<string, string>();
        private readonly object _gate = new object();

        public Action<string> OnRenew { get; set; }

        public Task<LeaseResult> TryAcquireAsync(string resourceName, TimeSpan leaseTime, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_leases.ContainsKey(resourceName))
                {
                    return Task.FromResult(LeaseResult.Failure());
                }

                var leaseId = Guid.NewGuid().ToString();
                _leases.Add(resourceName, leaseId);
                return Task.FromResult(LeaseResult.Success(leaseId));
            }
        }

        public Task<LeaseResult> RenewAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            OnRenew?.Invoke(resourceName);
            lock (_gate)
            {
                if (_leases.TryGetValue(resourceName, out var current) && current == leaseId)
                {
                    return Task.FromResult(LeaseResult.Success(leaseId));
                }

                return Task.FromResult(LeaseResult.Failure());
            }
        }

        public Task<bool> ReleaseAsync(string resourceName, string leaseId, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_leases.TryGetValue(resourceName, out var current) && current == leaseId)
                {
                    return Task.FromResult(_leases.Remove(resourceName));
                }

                return Task.FromResult(false);
            }
        }

        public void Expire(string resourceName)
        {
            lock (_gate)
            {
                _leases.Remove(resourceName);
            }
        }
    }
}
