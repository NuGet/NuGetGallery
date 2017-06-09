// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutoCompleteDatabasePackageVersionsQuery
        : AutoCompleteDatabaseQuery, IAutoCompletePackageVersionsQuery
    {
        private const string _sqlFormat = @"SELECT p.[Version]
FROM Packages p (NOLOCK)
	JOIN PackageRegistrations pr (NOLOCK) on pr.[Key] = p.PackageRegistrationKey
WHERE p.[Deleted] <> 1 AND {0} AND pr.ID = {{0}}
	{1}";

        public AutoCompleteDatabasePackageVersionsQuery(IEntitiesContext entities)
            : base(entities)
        {
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
            
            // Create SQL filter on SemVerLevel
            // By default, we filter out SemVer v2.0.0 package versions.
            var semVerLevelSqlFilter = "p.[SemVerLevelKey] IS NULL";
            if (!string.IsNullOrEmpty(semVerLevel))
            {
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
                if (semVerLevelKey == SemVerLevelKey.SemVer2)
                {
                    semVerLevelSqlFilter = $"(p.[SemVerLevelKey] IS NULL OR p.[SemVerLevelKey] <= {SemVerLevelKey.SemVer2})";
                }
            }

            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = 0";
            }
            
            return RunSqlQuery(string.Format(CultureInfo.InvariantCulture, _sqlFormat, semVerLevelSqlFilter, prereleaseFilter), id);
        }
    }
}