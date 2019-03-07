// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class CweQueryStringValidator
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMinutes(1);

        // When querying by CWE-ID, the validated search term may be properly prefixed already.
        // For numbers, we wait for 2 numeric characters. 
        private static readonly Regex QueryByIdValidationRegex = new Regex(
            @"(CWE-)?(?<query>\d{2,})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline,
            RegexTimeout);

        // When querying by Name, the validated search term will NOT be prefixed.
        // For text, we wait for 4 characters.
        private static readonly Regex QueryByNameValidationRegex = new Regex(
            @"\w{4,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline,
            RegexTimeout);

        /// <summary>
        /// Validates a query string against CWE-ID formatting and query rules.
        /// </summary>
        public static bool TryValidate(string queryString, out CweQueryMethod queryMethod, out string validatedQueryString)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                validatedQueryString = null;
                queryMethod = CweQueryMethod.Unknown;
                return false;
            }

            // Autocomplete can be for IDs starting with the number entered. 
            // If it's a text, then the suggestion would be based on the weakness name (title).

            var queryByIdMatch = QueryByIdValidationRegex.Match(queryString);
            if (queryByIdMatch.Success)
            {
                // The user intended to query by CWE-ID.
                var numericPartString = queryByIdMatch.Groups["query"].Value;

                // Return the validated CWE ID (including the prefix for consistency and for db querying).
                validatedQueryString = $"{Cwe.IdPrefix}{numericPartString}";

                queryMethod = CweQueryMethod.ByCweId;
                return true;
            }
            else
            {
                var queryByNameMatch = QueryByNameValidationRegex.Match(queryString);
                if (queryByNameMatch.Success)
                {
                    // The user intended to query by Name. Treat the query as a textual one.
                    validatedQueryString = queryString;
                    queryMethod = CweQueryMethod.ByName;
                    return true;
                }

                validatedQueryString = null;
                queryMethod = CweQueryMethod.Unknown;
                return false;
            }
        }
    }
}