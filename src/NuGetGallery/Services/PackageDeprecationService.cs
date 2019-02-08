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
        private IEntityRepository<PackageDeprecation> _deprecationRepository;
        private IEntityRepository<Cve> _cveRepository;
        private IEntityRepository<Cwe> _cweRepository;

        public PackageDeprecationService(
            IEntityRepository<PackageDeprecation> deprecationRepository,
            IEntityRepository<Cve> cveRepository,
            IEntityRepository<Cwe> cweRepository)
        {
            _deprecationRepository = deprecationRepository;
            _cveRepository = cveRepository;
            _cweRepository = cweRepository;
        }

        public async Task UpdateDeprecation(
            IReadOnlyCollection<Package> packages,
            bool isVulnerable,
            bool isLegacy,
            bool isOther,
            IReadOnlyCollection<Cve> cves,
            decimal? cvssRating,
            IReadOnlyCollection<Cwe> cwes,
            PackageRegistration alternatePackageRegistration,
            Package alternatePackage,
            string customMessage,
            bool shouldUnlist)
        {
            UpdatePackageDeprecation deprecatePackage;
            if (packages.Count() == 1)
            {
                // Updating a single package's deprecation does a replace.
                deprecatePackage = ReplaceDeprecation;
            }
            else
            {
                // Updating multiple packages combines the new state with the existing state for each package.
                deprecatePackage = CombineDeprecation;
            }

            deprecatePackage(
                packages,
                isVulnerable,
                isLegacy,
                isOther,
                cves,
                cvssRating,
                cwes,
                alternatePackageRegistration,
                alternatePackage,
                customMessage,
                shouldUnlist);

            await _deprecationRepository.CommitChangesAsync();
        }

        private delegate void UpdatePackageDeprecation(
            IReadOnlyCollection<Package> packages,
            bool isVulnerable,
            bool isLegacy,
            bool isOther,
            IReadOnlyCollection<Cve> cves,
            decimal? cvssRating,
            IReadOnlyCollection<Cwe> cwes,
            PackageRegistration alternatePackageRegistration,
            Package alternatePackage,
            string customMessage,
            bool shouldUnlist);

        private void CombineDeprecation(
            IReadOnlyCollection<Package> packages,
            bool isVulnerable,
            bool isLegacy,
            bool isOther,
            IReadOnlyCollection<Cve> cves,
            decimal? cvssRating,
            IReadOnlyCollection<Cwe> cwes,
            PackageRegistration alternatePackageRegistration,
            Package alternatePackage,
            string customMessage,
            bool shouldUnlist)
        {
            var shouldDelete = !(isVulnerable || isLegacy || isOther);
            var deprecations = new List<PackageDeprecation>();
            foreach (var package in packages)
            {
                var deprecation = package.Deprecations.SingleOrDefault();

                if (shouldDelete)
                {
                    package.Deprecations.Remove(deprecation);
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
                    }

                    if (isVulnerable)
                    {
                        deprecation.Status |= PackageDeprecationStatus.Vulnerable;
                    }

                    if (isLegacy)
                    {
                        deprecation.Status |= PackageDeprecationStatus.Legacy;
                    }

                    if (isOther)
                    {
                        deprecation.Status |= PackageDeprecationStatus.Other;
                    }

                    foreach (var cve in cves)
                    {
                        deprecation.Cves.Add(cve);
                    }

                    if (cvssRating != null)
                    {
                        deprecation.CvssRating = cvssRating;
                    }

                    foreach (var cwe in cwes)
                    {
                        deprecation.Cwes.Add(cwe);
                    }

                    if (alternatePackageRegistration != null)
                    {
                        deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                    }

                    if (alternatePackage != null)
                    {
                        deprecation.AlternatePackage = alternatePackage;
                    }

                    if (string.IsNullOrEmpty(customMessage))
                    {
                        deprecation.CustomMessage = customMessage;
                    }

                    if (shouldUnlist)
                    {
                        package.Listed = false;
                    }
                }

                deprecations.Add(deprecation);
            }

            if (shouldDelete)
            {
                _deprecationRepository.DeleteOnCommit(deprecations);
            }
            else
            {
                _deprecationRepository.InsertOnCommit(deprecations);
            }
        }

        private void ReplaceDeprecation(
            IReadOnlyCollection<Package> packages,
            bool isVulnerable,
            bool isLegacy,
            bool isOther,
            IReadOnlyCollection<Cve> cves,
            decimal? cvssRating,
            IReadOnlyCollection<Cwe> cwes,
            PackageRegistration alternatePackageRegistration,
            Package alternatePackage,
            string customMessage,
            bool shouldUnlist)
        {
            var status = PackageDeprecationStatus.NotDeprecated;
            if (isVulnerable)
            {
                status |= PackageDeprecationStatus.Vulnerable;
            }

            if (isLegacy)
            {
                status |= PackageDeprecationStatus.Legacy;
            }

            if (isOther)
            {
                status |= PackageDeprecationStatus.Other;
            }

            var shouldDelete = status == PackageDeprecationStatus.NotDeprecated;
            var deprecations = new List<PackageDeprecation>();
            foreach (var package in packages)
            {
                var deprecation = package.Deprecations.SingleOrDefault();
                if (shouldDelete)
                {
                    package.Deprecations.Remove(deprecation);
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
                    }

                    deprecation.Status = status;
                    foreach (var cve in cves)
                    {
                        deprecation.Cves.Add(cve);
                    }
                    deprecation.CvssRating = cvssRating;
                    foreach (var cwe in cwes)
                    {
                        deprecation.Cwes.Add(cwe);
                    }
                    deprecation.AlternatePackageRegistration = alternatePackageRegistration;
                    deprecation.AlternatePackage = alternatePackage;
                    deprecation.CustomMessage = customMessage;

                    if (shouldUnlist)
                    {
                        package.Listed = false;
                    }
                }

                deprecations.Add(deprecation);
            }

            if (shouldDelete)
            {
                _deprecationRepository.DeleteOnCommit(deprecations);
            }
            else
            {
                _deprecationRepository.InsertOnCommit(deprecations);
            }
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