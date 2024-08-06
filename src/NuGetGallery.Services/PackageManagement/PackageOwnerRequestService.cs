﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageOwnerRequestService : IPackageOwnerRequestService
    {
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;

        public PackageOwnerRequestService(IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository)
        {
            _packageOwnerRequestRepository = packageOwnerRequestRepository ?? throw new ArgumentNullException(nameof(packageOwnerRequestRepository));
        }

        public PackageOwnerRequest GetPackageOwnershipRequest(PackageRegistration package, User newOwner, string token)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var request = GetPackageOwnershipRequests(package: package, newOwner: newOwner).FirstOrDefault();
            if (request == null)
            {
                return null;
            }

            return request.ConfirmationCode == token ? request : null;
        }

        public IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(
            PackageRegistration package = null,
            User requestingOwner = null,
            User newOwner = null)
        {
            return GetPackageOwnershipRequests(includeUsers: false, package, requestingOwner, newOwner);
        }

        public IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequestsWithUsers(
            PackageRegistration package = null,
            User requestingOwner = null,
            User newOwner = null)
        {
            return GetPackageOwnershipRequests(includeUsers: true, package, requestingOwner, newOwner);
        }

        private IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(
            bool includeUsers,
            PackageRegistration package,
            User requestingOwner,
            User newOwner)
        {
            var query = _packageOwnerRequestRepository.GetAll().Include(e => e.PackageRegistration);

            if (includeUsers)
            {
                query = query
                    .Include(x => x.RequestingOwner)
                    .Include(x => x.NewOwner);
            }

            if (package != null)
            {
                query = query.Where(r => r.PackageRegistrationKey == package.Key);
            }

            if (requestingOwner != null)
            {
                query = query.Where(r => r.RequestingOwnerKey == requestingOwner.Key);
            }

            if (newOwner != null)
            {
                query = query.Where(r => r.NewOwnerKey == newOwner.Key);
            }

            return query.ToArray();
        }

        public async Task<PackageOwnerRequest> AddPackageOwnershipRequest(PackageRegistration package, User requestingOwner, User newOwner)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (requestingOwner == null)
            {
                throw new ArgumentNullException(nameof(requestingOwner));
            }

            if (newOwner == null)
            {
                throw new ArgumentNullException(nameof(newOwner));
            }

            var existingRequest = GetPackageOwnershipRequests(package: package, newOwner: newOwner).FirstOrDefault();
            if (existingRequest != null)
            {
                return existingRequest;
            }

            var newRequest = new PackageOwnerRequest
            {
                PackageRegistrationKey = package.Key,
                RequestingOwnerKey = requestingOwner.Key,
                NewOwnerKey = newOwner.Key,
                ConfirmationCode = CryptographyService.GenerateToken(),
                RequestDate = DateTime.UtcNow
            };

            _packageOwnerRequestRepository.InsertOnCommit(newRequest);
            await _packageOwnerRequestRepository.CommitChangesAsync();

            return newRequest;
        }

        public async Task DeletePackageOwnershipRequest(PackageOwnerRequest request, bool commitChanges = true)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _packageOwnerRequestRepository.DeleteOnCommit(request);

            if (commitChanges)
            {
                await _packageOwnerRequestRepository.CommitChangesAsync();
            }
        }
    }
}