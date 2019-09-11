// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutocompleteDatabasePackageIdsQuery : IAutocompletePackageIdsQuery
    {
        private readonly IEntitiesContext _entitiesContext;

        public AutocompleteDatabasePackageIdsQuery(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public Task<IEnumerable<string>> Execute(
            string partialId,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            var query = _entitiesContext.Packages
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

            // filters added for partialId
            if (!string.IsNullOrWhiteSpace(partialId))
            {
                query = query.Where(p => p.PackageRegistration.Id.StartsWith(partialId))
                    .OrderBy(p => p.PackageRegistration.Id);
            }
            else
            {
                query = query.OrderByDescending(p => p.PackageRegistration.DownloadCount);
            }

            // last default filter
            // this query returns 30 package ids at most
            var queryResult = query.Take(30).GroupBy(p => p.PackageRegistration.Id).Select(group => group.Key);

            // return the result of the query
            return Task.FromResult(queryResult.AsEnumerable<string>());
        }
    }
}