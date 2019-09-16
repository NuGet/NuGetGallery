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
    public class AutocompleteDatabasePackageVersionsQuery : IAutocompletePackageVersionsQuery
    {
        private readonly IReadOnlyEntityRepository<Package> _packageRepository;

        public AutocompleteDatabasePackageVersionsQuery(IReadOnlyEntityRepository<Package> packageRepository)
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
        }

        public Task<IReadOnlyList<string>> Execute(
            string id,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // default filters
            var query = _packageRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id == id)
                .Where(p => p.PackageStatusKey == PackageStatus.Available && p.Listed)
                .Where(SemVerLevelKey.IsPackageCompliantWithSemVerLevelPredicate(semVerLevel));

            // prerelease filter
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                query = query.Where(p => !p.IsPrerelease);
            }

            var versions = query.Select(p => p.Version).ToList();

            // return the result of the query
            return Task.FromResult<IReadOnlyList<string>>(versions);
        }
    }
}