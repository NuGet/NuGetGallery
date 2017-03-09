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
WHERE p.[SemVerLevelKey] IS NULL AND pr.ID = {{0}}
	{0}";
        
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

            if (!string.IsNullOrEmpty(semVerLevel))
            {
                // todo: create SQL filter on SemVerLevel
            }

            var prereleaseFilter = string.Empty;
            if (!includePrerelease.HasValue || !includePrerelease.Value)
            {
                prereleaseFilter = "AND p.IsPrerelease = 0";
            }

            // todo: apply SQL filter on SemVerLevel
            return RunSqlQuery(string.Format(CultureInfo.InvariantCulture, _sqlFormat, prereleaseFilter), id);
        }
    }
}