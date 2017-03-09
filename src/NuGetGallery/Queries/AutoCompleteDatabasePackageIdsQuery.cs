// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutoCompleteDatabasePackageIdsQuery
        : AutoCompleteDatabaseQuery, IAutoCompletePackageIdsQuery
    {
        private const string _partialIdSqlFormat = @"SELECT TOP 30 pr.ID
FROM Packages p (NOLOCK)
    JOIN PackageRegistrations pr (NOLOCK) on pr.[Key] = p.PackageRegistrationKey
WHERE p.[SemVerLevelKey] IS NULL AND pr.ID LIKE {{0}}
    {0}
GROUP BY pr.ID
ORDER BY pr.ID";

        private const string _noPartialIdSql = @"SELECT TOP 30 pr.ID
FROM Packages p (NOLOCK)
    JOIN PackageRegistrations pr (NOLOCK) on pr.[Key] = p.PackageRegistrationKey
WHERE  p.[SemVerLevelKey] IS NULL 
GROUP BY pr.ID
ORDER BY MAX(pr.DownloadCount) DESC";
        
        public AutoCompleteDatabasePackageIdsQuery(IEntitiesContext entities)
            : base(entities)
        {
        }

        public Task<IEnumerable<string>> Execute(
            string partialId,
            bool? includePrerelease = false,
            string semVerLevel = null)
        {
            if (!string.IsNullOrEmpty(semVerLevel))
            {
                // todo: create SQL filter on SemVerLevel
            }

            if (string.IsNullOrWhiteSpace(partialId))
            {
                // todo: apply SQL filter on SemVerLevel
                return RunSqlQuery(_noPartialIdSql);
            }

            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                // todo: apply SQL filter on SemVerLevel
                prereleaseFilter = "AND p.IsPrerelease = {1}";
            }

            var sql = string.Format(CultureInfo.InvariantCulture, _partialIdSqlFormat, prereleaseFilter);

            return RunSqlQuery(sql, partialId + "%", includePrerelease ?? false);
        }
    }
}