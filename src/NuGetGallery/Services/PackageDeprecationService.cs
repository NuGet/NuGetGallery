// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class PackageDeprecationService : IPackageDeprecationService
    {
        private readonly IEntityRepository<PackageDeprecation> _deprecationRepository;
        private readonly IEntityRepository<Cve> _cveRepository;
        private readonly IEntityRepository<Cwe> _cweRepository;

        public PackageDeprecationService(
           IEntityRepository<PackageDeprecation> deprecationRepository,
           IEntityRepository<Cve> cveRepository,
           IEntityRepository<Cwe> cweRepository)
        {
            _deprecationRepository = deprecationRepository ?? throw new ArgumentNullException(nameof(deprecationRepository));
            _cveRepository = cveRepository ?? throw new ArgumentNullException(nameof(cveRepository));
            _cweRepository = cweRepository ?? throw new ArgumentNullException(nameof(cweRepository));
        }

        public async Task UpdateDeprecation(
           IReadOnlyCollection<Package> packages,
           PackageDeprecationStatus status,
           IReadOnlyCollection<Cve> cves,
           decimal? cvssRating,
           IReadOnlyCollection<Cwe> cwes,
           PackageRegistration alternatePackageRegistration,
           Package alternatePackage,
           string customMessage,
           bool shouldUnlist)
        {
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
                    
                    if (status.HasFlag(PackageDeprecationStatus.Vulnerable))
                    {
                        deprecation.Cves.Clear();
                        foreach (var cve in cves)
                        {
                            deprecation.Cves.Add(cve);
                        }

                        deprecation.CvssRating = cvssRating;

                        deprecation.Cwes.Clear();
                        foreach (var cwe in cwes)
                        {
                            deprecation.Cwes.Add(cwe);
                        }
                    }

                    if (status.HasFlag(PackageDeprecationStatus.Legacy))
                    {
                        deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                        deprecation.AlternatePackage = alternatePackage;
                    }

                    deprecation.CustomMessage = customMessage;

                    if (shouldUnlist)
                    {
                        package.Listed = false;
                    }
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

        public IReadOnlyCollection<Cve> GetCvesById(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            return _cveRepository.GetAll()
               .Where(c => ids.Contains(c.CveId))
               .ToList();
        }

        public IReadOnlyCollection<Cwe> GetCwesById(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            return _cweRepository.GetAll()
               .Where(c => ids.Contains(c.CweId))
               .ToList();
        }
    }
}