// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageDeprecationService : IPackageDeprecationService
    {
        private readonly IEntityRepository<PackageDeprecation> _deprecationRepository;

        public PackageDeprecationService(
           IEntityRepository<PackageDeprecation> deprecationRepository)
        {
            _deprecationRepository = deprecationRepository ?? throw new ArgumentNullException(nameof(deprecationRepository));
        }

        public async Task UpdateDeprecation(
           IReadOnlyCollection<Package> packages,
           PackageDeprecationStatus status,
           PackageRegistration alternatePackageRegistration,
           Package alternatePackage,
           string customMessage)
        {
            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
            var deprecations = new List<PackageDeprecation>();
            foreach (var package in packages)
            {
                var deprecation = package.Deprecations.SingleOrDefault();
                if (shouldDelete)
                {
                    if (deprecation != null)
                    {
                        package.Deprecations.Remove(deprecation);
                        deprecations.Add(deprecation);
                    }
                }
                else
                {
                    if (deprecation == null)
                    {
                        deprecation = new PackageDeprecation
                        {
                            Package = package
                        };

                        package.Deprecations.Add(deprecation);
                        deprecations.Add(deprecation);
                    }

                    deprecation.Status = status;

                    deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                    deprecation.AlternatePackage = alternatePackage;

                    deprecation.CustomMessage = customMessage;
                }
            }

            if (shouldDelete)
            {
                _deprecationRepository.DeleteOnCommit(deprecations);
            }
            else
            {
                _deprecationRepository.InsertOnCommit(deprecations);
            }

            await _deprecationRepository.CommitChangesAsync();
        }

        public PackageDeprecation GetDeprecationByPackage(Package package)
        {
            return _deprecationRepository.GetAll()
                .Include(d => d.AlternatePackage.PackageRegistration)
                .Include(d => d.AlternatePackageRegistration)
                .SingleOrDefault(d => d.PackageKey == package.Key);
        }
    }
}