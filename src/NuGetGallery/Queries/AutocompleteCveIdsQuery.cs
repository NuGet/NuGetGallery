// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class AutocompleteCveIdsQuery
        : IAutoCompleteCveIdsQuery
    {
        // Search results should be limited anywhere between 5 - 10 results.
        private const int _maxResults = 5;
        private readonly IEntitiesContext _entitiesContext;

        public AutocompleteCveIdsQuery(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IReadOnlyCollection<CveIdAutocompleteQueryResult> Execute(string partialId)
        {
            if (string.IsNullOrEmpty(partialId))
            {
                throw new ArgumentNullException(nameof(partialId));
            }

            var validatedPartialId = ValidatePartialCveIdFormat(partialId);

            // Query the database.
            // Only include listed CVE entities.
            var queryResults = _entitiesContext.Cves
                .Where(e => e.CveId.StartsWith(validatedPartialId) && e.Listed == true)
                .OrderBy(e => e.CveId)
                .Take(_maxResults)
                .ToList();

            return queryResults
                .Select(e => new CveIdAutocompleteQueryResult(e.CveId, e.Description))
                .ToList();
        }

        private string ValidatePartialCveIdFormat(string partialId)
        {
            // The user input will be interpreted as a single CVE Id.
            // The user may choose to not type the CVE Id prefix "CVE-".
            // Accepted user input format is "CVE-{year}-xxxxxxx" or just "{year}-xxxxxxx".

            // Strip off the leading "CVE-" if present.
            var remainingSearchTerm = CveIdHelper.RemoveCveIdPrefix(partialId);

            if (remainingSearchTerm.Length < 4)
            {
                throw new FormatException(Strings.AutocompleteCveIds_FormatException);
            }

            // Try parsing the {year} part of the CVE Id.
            if (!int.TryParse(remainingSearchTerm.Substring(0, 4), out var year))
            {
                // Invalid search term. Should be formatted as "CVE-{year}-xxxx" or just "{year}-xxxx".
                throw new FormatException(Strings.AutocompleteCveIds_FormatException);
            }

            if (remainingSearchTerm.Length == 4)
            {
                return $"{Cve.IdPrefix}{year}";
            }
            else
            {
                // If the remaining part is more than 4 characters, it should have the format "{year}-xxxxxxx".
                if (remainingSearchTerm[4] != '-')
                {
                    throw new FormatException(Strings.AutocompleteCveIds_FormatException);
                }

                remainingSearchTerm = remainingSearchTerm.Substring(4, remainingSearchTerm.Length - 4);
            }

            // Return the validated CVE ID (including the prefix for consistency and for db querying).
            return $"{Cve.IdPrefix}{year}{remainingSearchTerm}";
        }
    }
}