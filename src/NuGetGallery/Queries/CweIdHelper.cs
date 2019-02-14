// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class CweIdHelper
    {
        /// <summary>
        /// Returns the numeric part of the <see cref="Cve.CveId"/> as an integer.
        /// Useful to determine sort order.
        /// </summary>
        /// <returns>
        /// Returns <c>null</c> if the provided <paramref name="cweId"/> does not contain a valid numeric part; 
        /// otherwise returns the 32-bit integer representation of the numeric part.
        /// </returns>
        public static int? GetCweIdNumericPartAsInteger(string cweId)
        {
            try
            {
                var numericPartAsString = GetCweIdNumericPartAsString(cweId);
                if (int.TryParse(numericPartAsString, out var numericPartAsInteger))
                {
                    return numericPartAsInteger;
                }

                return null;
            }
            catch (NotSupportedException)
            {
                // If the CWE ID provided did not start with the 'CWE-' prefix,
                // attempt to parse the remainder of the ID as an integer.
                if (int.TryParse(cweId, out var numericPart))
                {
                    return numericPart;
                }

                return null;
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether a CWE-ID starts with the CWE-ID prefix.
        /// </summary>
        /// <param name="cweId"></param>
        /// <returns><c>True</c> if the <paramref name="cweId"/> starts with the <see cref="Cwe.IdPrefix"/>; otherwise <c>false</c>.</returns>
        internal static bool StartsWithCweIdPrefix(string cweId)
        {
            if (string.IsNullOrWhiteSpace(cweId))
            {
                return false;
            }

            return cweId.StartsWith(Cwe.IdPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the numeric part of the <see cref="Cve.CveId"/> as a string.
        /// </summary>
        /// <returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the provided <paramref name="cweId"/> does not start with the <see cref="Cve.IdPrefix"/>.
        /// </exception>
        private static string GetCweIdNumericPartAsString(string cweId)
        {
            if (!StartsWithCweIdPrefix(cweId))
            {
                throw new NotSupportedException();
            }

            return cweId.Substring(Cwe.IdPrefix.Length, cweId.Length - Cwe.IdPrefix.Length);
        }
    }
}