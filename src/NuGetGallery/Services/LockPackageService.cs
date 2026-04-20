// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;

namespace NuGetGallery
{
    public class LockPackageService : ILockPackageService
    {
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IAuditingService _auditingService;

        public LockPackageService(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IAuditingService auditingService)
        {
            _packageRegistrationRepository = packageRegistrationRepository ?? throw new ArgumentNullException(nameof(packageRegistrationRepository));
            _auditingService = auditingService ?? throw new ArgumentNullException(nameof(auditingService));
        }

        public async Task<LockPackageServiceResult> SetLockStateAsync(string packageId, bool isLocked)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID must not be null or empty.", nameof(packageId));
            }

            var packageRegistration = _packageRegistrationRepository
                .GetAll()
                .SingleOrDefault(pr => pr.Id == packageId);

            if (packageRegistration == null)
            {
                return LockPackageServiceResult.PackageNotFound;
            }

            if (packageRegistration.IsLocked != isLocked)
            {
                packageRegistration.IsLocked = isLocked;

                await _auditingService.SaveAuditRecordAsync(new PackageRegistrationAuditRecord(
                    packageRegistration,
                    isLocked ? AuditedPackageRegistrationAction.Lock : AuditedPackageRegistrationAction.Unlock,
                    owner: null));

                await _packageRegistrationRepository.CommitChangesAsync();
            }

            return LockPackageServiceResult.Success;
        }
    }
}
