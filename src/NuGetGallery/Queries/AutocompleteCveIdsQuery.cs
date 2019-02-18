// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class AutocompleteCveIdsQuery
        : IAutocompleteCveIdsQuery
    {
        // Search results should be limited anywhere between 5 - 10 results.
        private const int MaxResults = 5;

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);
        private static readonly Regex ValidationRegex = new Regex(
            @"(CVE-)?(?<query>\d{4}(-\d*)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline,
            RegexTimeout);

        private readonly IEntitiesContext _entitiesContext;

        public AutocompleteCveIdsQuery(IEntitiesContext entitiesContext)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
        }

        public IReadOnlyCollection<AutocompleteCveIdQueryResult> Execute(string partialId)
        {
            if (string.IsNullOrEmpty(partialId))
            {
                throw new ArgumentNullException(nameof(partialId));
            }

            var validatedPartialId = ValidatePartialCveIdFormat(partialId);

            // Query the database.
            // Only include listed CVE entities.
            var queryResults = _entitiesContext.Cves
                .Where(e => e.CveId.StartsWith(validatedPartialId) && e.Listed)
                .OrderBy(e => e.CveId)
                .Take(MaxResults)
                .ToList();

            return queryResults
                .Select(e => new AutocompleteCveIdQueryResult(e.CveId, e.Description))
                .ToList();
        }

        private string ValidatePartialCveIdFormat(string partialId)
        {
            // The user input will be interpreted as a single CVE Id.
            // The user may choose to not type the CVE Id prefix "CVE-".
            // Accepted user input format is "CVE-{year}-xxxxxxx" or just "{year}-xxxxxxx".

            var match = ValidationRegex.Match(partialId);

            if (match.Value == string.Empty)
            {
                throw new FormatException(Strings.AutocompleteCveIds_FormatException);
            }

            var query = match.Groups["query"];

            // Return the validated CVE ID (including the prefix for consistency and for db querying).
            return $"{Cve.IdPrefix}{query}";
        }
    }
}