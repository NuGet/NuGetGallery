// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageOwnerRequestService
    {
        /// <summary>
        /// Gets <see cref="PackageOwnerRequest"/>s that match the provided conditions. User entities on the returned requests are not populated.
        /// </summary>
        /// <param name="package">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that are for this package.</param>
        /// <param name="requestingOwner">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that were requested by this owner.</param>
        /// <param name="newOwner">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that are for this user to become an owner.</param>
        /// <returns>An <see cref="IEnumerable{PackageOwnerRequest}"/> containing all objects that matched the conditions.</returns>
        IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(PackageRegistration package = null, User requestingOwner = null, User newOwner = null);

        /// <summary>
        /// Gets <see cref="PackageOwnerRequest"/>s that match the provided conditions. User entities on the returned requests are populated.
        /// </summary>
        /// <param name="package">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that are for this package.</param>
        /// <param name="requestingOwner">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that were requested by this owner.</param>
        /// <param name="newOwner">If nonnull, only returns <see cref="PackageOwnerRequest"/>s that are for this user to become an owner.</param>
        /// <returns>An <see cref="IEnumerable{PackageOwnerRequest}"/> containing all objects that matched the conditions.</returns>
        IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequestWithUsers(PackageRegistration package = null, User requestingOwner = null, User newOwner = null);

        /// <summary>
        /// Checks if the pending owner has a request for this package which matches the specified token.
        /// </summary>
        /// <param name="package">Package associated with the request.</param>
        /// <param name="pendingOwner">Pending owner for the request.</param>
        /// <param name="token">Token generated for the owner request.</param>
        /// <returns>The <see cref="PackageOwnerRequest"/> if one exists or null otherwise.</returns>
        PackageOwnerRequest GetPackageOwnershipRequest(PackageRegistration package, User pendingOwner, string token);

        /// <summary>
        /// Creates a <see cref="PackageOwnerRequest"/> with the given parameters.
        /// </summary>
        /// <param name="package">The package that the <see cref="PackageOwnerRequest"/> will pertain to.</param>
        /// <param name="requestingOwner">The owner creating this <see cref="PackageOwnerRequest"/>.</param>
        /// <param name="newOwner">The user to be added as an owner by this <see cref="PackageOwnerRequest"/>.</param>
        /// <returns>A <see cref="PackageOwnerRequest"/> with the given parameters.</returns>
        Task<PackageOwnerRequest> AddPackageOwnershipRequest(PackageRegistration package, User requestingOwner, User newOwner);

        /// <summary>
        /// Deletes the provided <see cref="PackageOwnerRequest"/>.
        /// </summary>
        /// <param name="request">The <see cref="PackageOwnerRequest"/> to delete.</param>
        Task DeletePackageOwnershipRequest(PackageOwnerRequest request, bool commitChanges = true);
    }
}
