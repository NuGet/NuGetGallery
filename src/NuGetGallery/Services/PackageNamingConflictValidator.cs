// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery
{
    public class PackageNamingConflictValidator
        : IPackageNamingConflictValidator
    {
        private readonly IEntityRepository<PackageRegistration> _packageRegistrationRepository;
        private readonly IEntityRepository<Package> _packageRepository;

        public PackageNamingConflictValidator(
            IEntityRepository<PackageRegistration> packageRegistrationRepository,
            IEntityRepository<Package> packageRepository)
        {
            _packageRegistrationRepository = packageRegistrationRepository;
            _packageRepository = packageRepository;
        }

        public bool TitleConflictsWithExistingRegistrationId(string registrationId, string packageTitle)
        {
            if (string.IsNullOrEmpty(registrationId))
            {
                throw new ArgumentNullException(nameof(registrationId));
            }

            if (!string.IsNullOrEmpty(packageTitle))
            {
                var cleanedTitle = packageTitle.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(cleanedTitle))
                {
                    var packageRegistration = _packageRegistrationRepository.GetAll()
                        .SingleOrDefault(pr => pr.Id.ToLower() == cleanedTitle);

                    if (packageRegistration != null
                        && !String.Equals(packageRegistration.Id, registrationId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IdConflictsWithExistingPackageTitle(string registrationId)
        {
            if (string.IsNullOrEmpty(registrationId))
            {
                throw new ArgumentNullException(nameof(registrationId));
            }

            registrationId = registrationId.ToLowerInvariant();

            return _packageRepository.GetAll()
                .Any(p => !p.Deleted && p.Title.ToLower() == registrationId);
        }
    }
}