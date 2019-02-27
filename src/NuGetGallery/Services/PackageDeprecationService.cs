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

        public Task<IReadOnlyCollection<Cve>> GetOrCreateCvesByIdAsync(IEnumerable<string> ids, bool commitChanges)
        {
            return GetOrCreateVulnerabilityDetailById(
                ids,
                commitChanges,
                _cveRepository,
                cve => cve.CveId,
                id => new Cve
                {
                    CveId = id,
                    Listed = false,
                    Status = CveStatus.Unknown
                });
        }

        public Task<IReadOnlyCollection<Cwe>> GetOrCreateCwesByIdAsync(IEnumerable<string> ids, bool commitChanges)
        {
            return GetOrCreateVulnerabilityDetailById(
                ids, 
                commitChanges, 
                _cweRepository, 
                cwe => cwe.CweId, 
                id => new Cwe
                {
                    CweId = id,
                    Listed = false,
                    Status = CweStatus.Unknown
                });
        }

        private async Task<IReadOnlyCollection<TVulnerabilityDetail>> GetOrCreateVulnerabilityDetailById<TVulnerabilityDetail>(
            IEnumerable<string> ids, 
            bool commitChanges, 
            IEntityRepository<TVulnerabilityDetail> repository,
            Func<TVulnerabilityDetail, string> getId,
            Func<string, TVulnerabilityDetail> createDetail)
            where TVulnerabilityDetail : class, new()
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var details = repository.GetAll()
               .Where(c => ids.Contains(getId(c)))
               .ToList();

            var addedDetails = new List<TVulnerabilityDetail>();
            foreach (var missingId in ids.Where(i => !details.Any(c => getId(c) == i)))
            {
                var detail = createDetail(missingId);
                addedDetails.Add(detail);
                details.Add(detail);
            }

            if (addedDetails.Any())
            {
                repository.InsertOnCommit(addedDetails);
                if (commitChanges)
                {
                    await repository.CommitChangesAsync();
                }
            }

            return details;
        }
    }
}