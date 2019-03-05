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
           string customMessage)
        {
            if (packages == null || !packages.Any())
            {
                throw new ArgumentException(nameof(packages));
            }

            if (cves == null)
            {
                throw new ArgumentNullException(nameof(cves));
            }

            if (cwes == null)
            {
                throw new ArgumentNullException(nameof(cwes));
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

        public async Task<IReadOnlyCollection<Cve>> GetOrCreateCvesByIdAsync(IEnumerable<string> ids, bool commitChanges)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var details = _cveRepository.GetAll()
               .Where(c => ids.Contains(c.CveId))
               .ToList();

            var addedDetails = new List<Cve>();
            foreach (var missingId in ids.Where(i => !details.Any(c => c.CveId == i)))
            {
                var detail = new Cve
                {
                    CveId = missingId,
                    Listed = false,
                    Status = CveStatus.Unknown
                };
                addedDetails.Add(detail);
                details.Add(detail);
            }

            if (addedDetails.Any())
            {
                _cveRepository.InsertOnCommit(addedDetails);
                if (commitChanges)
                {
                    await _cveRepository.CommitChangesAsync();
                }
            }

            return details;
        }

        public IReadOnlyCollection<Cwe> GetCwesById(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var cwes = _cweRepository.GetAll()
               .Where(c => ids.Contains(c.CweId))
               .ToList();

            if (ids.Any(i => !cwes.Any(c => i == c.CweId)))
            {
                throw new ArgumentException("Some IDs do not have a CWE associated with them!", nameof(ids));
            }

            return cwes;
        }

        public PackageDeprecation GetDeprecationByPackage(Package package)
        {
            return _deprecationRepository.GetAll()
                .Include(d => d.Cves)
                .Include(d => d.Cwes)
                .Include(d => d.AlternatePackage.PackageRegistration)
                .Include(d => d.AlternatePackageRegistration)
                .SingleOrDefault(d => d.PackageKey == package.Key);
        }
    }
}