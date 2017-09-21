// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class PackageOwnerRequestService : IPackageOwnerRequestService
    {
        private readonly IEntityRepository<PackageOwnerRequest> _packageOwnerRequestRepository;

        public PackageOwnerRequestService(IEntityRepository<PackageOwnerRequest> packageOwnerRequestRepository)
        {
            _packageOwnerRequestRepository = packageOwnerRequestRepository ?? throw new ArgumentNullException(nameof(packageOwnerRequestRepository));
        }

        public bool IsValidPackageOwnerRequest(PackageRegistration package, User newOwner, string token)
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
            return request != null && request.ConfirmationCode == token;
        }

        public IEnumerable<PackageOwnerRequest> GetPackageOwnershipRequests(PackageRegistration package = null, User requestingOwner = null, User newOwner = null)
        {
            var query = _packageOwnerRequestRepository.GetAll();

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

        public async Task DeletePackageOwnershipRequest(PackageOwnerRequest request)
        {
            _packageOwnerRequestRepository.DeleteOnCommit(request);
            await _packageOwnerRequestRepository.CommitChangesAsync();
        }
        
    }
}