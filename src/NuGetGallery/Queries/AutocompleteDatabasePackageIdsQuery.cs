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
    public class AutocompleteDatabasePackageIdsQuery : IAutocompletePackageIdsQuery
    {
        private readonly IReadOnlyEntityRepository<Package> _packageRepository;

        public AutocompleteDatabasePackageIdsQuery(IReadOnlyEntityRepository<Package> packageRepository)
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
        }

        public Task<IReadOnlyList<string>> Execute(
            string partialId,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            var query = _packageRepository.GetAll()
                .Include(p => p.PackageRegistration);
            
            // SemVerLevel filter
            if (SemVerLevelKey.ForSemVerLevel(semVerLevel) == SemVerLevelKey.SemVer2)
            {
                query = query.Where(p => p.SemVerLevelKey == SemVerLevelKey.SemVer2);
            }
            else
            {
                query = query.Where(p => !p.SemVerLevelKey.HasValue);
            }

            // prerelease filter
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                query = query.Where(p => !p.IsPrerelease);
            }

            var ids = new List<string>();

            // filters added for partialId
            if (!string.IsNullOrWhiteSpace(partialId))
            {
                ids = query.Where(p => p.PackageRegistration.Id.StartsWith(partialId))
                    .GroupBy(p => p.PackageRegistration.Id)
                    .Select(group => group.Key)
                    .OrderBy(id => id)
                    .Take(30)
                    .ToList();
            }
            else
            {
                ids = query.GroupBy(p => p.PackageRegistration.Id)
                    .Select(group => new
                    {
                        Id = group.Key,
                        MaxDownloadCount = group.Max(package => package.PackageRegistration.DownloadCount)
                    })
                    .OrderByDescending(group => group.MaxDownloadCount)
                    .Select(group => group.Id)
                    .Take(30)
                    .ToList();
            }

            return Task.FromResult<IReadOnlyList<string>>(ids);
        }
    }
}