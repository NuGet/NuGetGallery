// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Entities;

namespace NuGet.Services.Validation
{
    public class PackageCriteriaEvaluator : ICriteriaEvaluator<Package>
    {
        public bool IsMatch(ICriteria criteria, Package package)
        {
            // By default, match the package.
            var isMatch = true;

            // Apply the owners rules.
            if (criteria.ExcludeOwners != null)
            {
                var ownerUsernames = package
                    .PackageRegistration
                    .Owners
                    .Select(x => x.Username);

                if (criteria.ExcludeOwners.Intersect(ownerUsernames).Any())
                {
                    isMatch = false;
                }
            }

            // Apply the package ID pattern rules.
            if (criteria.IncludeIdPatterns != null)
            {
                foreach (var includeIdPattern in criteria.IncludeIdPatterns)
                {
                    var includeIdRegex = new Regex(
                        WildcardToRegex(includeIdPattern),
                        RegexOptions.IgnoreCase,
                        TimeSpan.FromSeconds(5));

                    if (includeIdRegex.IsMatch(package.PackageRegistration.Id))
                    {
                        isMatch = true;
                    }
                }
            }

            return isMatch;
        }

        /// <summary>
        /// Source: <see cref="https://stackoverflow.com/a/6907849"/>
        /// </summary>
        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex
                .Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
        }
    }
}
