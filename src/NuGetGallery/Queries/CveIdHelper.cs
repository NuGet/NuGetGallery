// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public static class CveIdHelper
    {
        /// <summary>
        /// Determines whether a CVE-ID starts with the CVE-ID prefix.
        /// </summary>
        /// <param name="cveId"></param>
        /// <returns><c>True</c> if the <paramref name="cveId"/> starts with the <see cref="Cve.IdPrefix"/>; otherwise <c>false</c>.</returns>
        public static bool StartsWithCveIdPrefix(string cveId)
        {
            if (string.IsNullOrWhiteSpace(cveId))
            {
                return false;
            }

            return cveId.StartsWith(Cve.IdPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes the <see cref="Cve.IdPrefix"/> from the provided <paramref name="partialId"/>, if present.
        /// </summary>
        public static string RemoveCveIdPrefix(string partialId)
        {
            if (StartsWithCveIdPrefix(partialId))
            {
                return partialId.Substring(Cve.IdPrefix.Length, partialId.Length - Cve.IdPrefix.Length);
            }

            return partialId;
        }
    }
}