// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Leases
{
    /// <summary>
    /// A service used for managing global leases. The primary implementation of this is based on Azure Blob Storage
    /// leases.
    /// </summary>
    public interface ILeaseService
    {
        /// <summary>
        /// Try to acquire a lease with the provided name. The resource name is a string used to identify the logical
        /// resource being leased. This name is case sensitive. A simple example of a resource name would be the
        /// concatenation of a lowercase package ID and lowercase normalized package version. Such a lease would allow
        /// the caller to operate exclusively on a package.
        /// </summary>
        /// <param name="resourceName">The resource name to lease.</param>
        /// <param name="leaseTime">The duration of the lease.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The lease result.</returns>
        Task<LeaseResult> TryAcquireAsync(string resourceName, TimeSpan leaseTime, CancellationToken cancellationToken);

        /// <summary>
        /// Release a lease that has already been acquired. If the lease has already been acquired by another thread or
        /// if the lease ID is invalid, this method will return false. If the lease is expired or still leased with the
        /// provided lease ID, this method will return true.
        /// </summary>
        /// <param name="resourceName">The resource name the existing lease is associated with.</param>
        /// <param name="leaseId">The lease ID obtained while acquiring the lease.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if the lease was gracefully released. False otherwise.</returns>
        Task<bool> ReleaseAsync(string resourceName, string leaseId, CancellationToken cancellationToken);

        /// <summary>
        /// Renew a lease that has already been acquired. If the lease has already been acquired by another thread or
        /// if the lease ID is invalid, this method will fail in renewing the leasse. If the lease is expired or still leased with the
        /// provided lease ID, this method will reset the expiration clock of existing lease.
        /// </summary>
        /// <param name="resourceName">The resource name the existing lease is associated with.</param>
        /// <param name="leaseId">The lease ID obtained while acquiring the lease.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The lease result.</returns>
        Task<LeaseResult> RenewAsync(string resourceName, string leaseId, CancellationToken cancellationToken);
    }
}