// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class CweQueryStringValidator
    {
        public static string Validate(string queryString, out CweQueryMethod queryMethod)
        {
            // Autocomplete can be for IDs starting with the number entered. 
            // If it's a text, then the suggestion would be based on the weakness name (title).

            queryMethod = DetermineQueryMethod(queryString, out var numericPartString);

            if (queryMethod == CweQueryMethod.ByCweId)
            {
                // Return the validated CWE ID (including the prefix for consistency and for db querying).
                return $"{Cwe.IdPrefix}{numericPartString}";
            }
            else
            {
                return queryString;
            }
        }

        private static CweQueryMethod DetermineQueryMethod(string queryString, out string numericPartString)
        {
            if (CweIdHelper.StartsWithCweIdPrefix(queryString))
            {
                // The user intended to query by CWE-ID.
                numericPartString = CweIdHelper.GetCweIdNumericPartAsString(queryString);

                return CweQueryMethod.ByCweId;
            }
            else
            {
                // Try parsing the numeric part of the CWE Id.
                var numericPart = CweIdHelper.GetCweIdNumericPartAsInteger(queryString);
                if (!numericPart.HasValue)
                {
                    // The user intended to query by Name. Treat the query as a textual one.
                    numericPartString = null;
                    return CweQueryMethod.ByName;
                }

                numericPartString = queryString;
                return CweQueryMethod.ByCweId;
            }
        }
    }
}