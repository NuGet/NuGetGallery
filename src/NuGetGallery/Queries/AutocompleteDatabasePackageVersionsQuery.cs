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
        private readonly IEntitiesContext _entitiesContext;

        public AutocompleteDatabasePackageVersionsQuery(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public Task<IEnumerable<string>> Execute(
            string id,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            // default filters
            var query = _entitiesContext.Packages
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageStatusKey == PackageStatus.Available && p.Listed && p.PackageRegistration.Id == id);

            // SemVerLevel filter
            if (SemVerLevelKey.ForSemVerLevel(semVerLevel) == SemVerLevelKey.SemVer2)
            {
                query = query.Where(p => !p.SemVerLevelKey.HasValue || p.SemVerLevelKey <= SemVerLevelKey.SemVer2);
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

            // return the result of the query
            return Task.FromResult(query.Select(p => p.Version).AsEnumerable<string>());
        }
    }
}