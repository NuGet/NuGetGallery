// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutocompleteDatabasePackageIdsQuery
        : AutocompleteDatabaseQuery, IAutocompletePackageIdsQuery
    {
        private const string _partialIdSqlFormat = @"SELECT TOP 30 pr.ID
FROM Packages p (NOLOCK)
    JOIN PackageRegistrations pr (NOLOCK) on pr.[Key] = p.PackageRegistrationKey
WHERE {0} AND pr.ID LIKE {{0}}
    {1}
GROUP BY pr.ID
ORDER BY pr.ID";

        private const string _noPartialIdSql = @"SELECT TOP 30 pr.ID
FROM Packages p (NOLOCK)
    JOIN PackageRegistrations pr (NOLOCK) on pr.[Key] = p.PackageRegistrationKey
WHERE  {0} 
GROUP BY pr.ID
ORDER BY MAX(pr.DownloadCount) DESC";
        
        public AutocompleteDatabasePackageIdsQuery(IEntitiesContext entities)
            : base(entities)
        {
        }

        public Task<IEnumerable<string>> Execute(
            string partialId,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            // Create SQL filter on SemVerLevel
            // By default, we filter out SemVer v2.0.0 package versions.
            var semVerLevelSqlFilter = "p.[SemVerLevelKey] IS NULL";
            if (!string.IsNullOrEmpty(semVerLevel))
            {
                var semVerLevelKey = SemVerLevelKey.ForSemVerLevel(semVerLevel);
                if (semVerLevelKey == SemVerLevelKey.SemVer2)
                {
                    semVerLevelSqlFilter = "p.[SemVerLevelKey] = " + SemVerLevelKey.SemVer2;
                }
            }

            if (string.IsNullOrWhiteSpace(partialId))
            {
                return RunSqlQuery(string.Format(CultureInfo.InvariantCulture, _noPartialIdSql, semVerLevelSqlFilter));
            }

            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = {1}";
            }

            var sql = string.Format(CultureInfo.InvariantCulture, _partialIdSqlFormat, semVerLevelSqlFilter, prereleaseFilter);

            return RunSqlQuery(sql, partialId + "%", includePrerelease ?? false);
        }
    }
}